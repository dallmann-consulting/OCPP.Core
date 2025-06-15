/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleStopTransaction(OCPPMessage msgIn, OCPPMessage msgOut, OCPPMiddleware ocppMiddleware)
        {
            string errorCode = null;
            StopTransactionResponse stopTransactionResponse = new StopTransactionResponse();
            stopTransactionResponse.IdTagInfo = new IdTagInfo();

            try
            {
                Logger.LogTrace("Processing stopTransaction request...");
                StopTransactionRequest stopTransactionRequest = DeserializeMessage<StopTransactionRequest>(msgIn);
                Logger.LogTrace("StopTransaction => Message deserialized");

                string idTag = CleanChargeTagId(stopTransactionRequest.IdTag, Logger);

                Transaction transaction = null;
                ChargeTag chargeTag = null;
                try
                {
                    transaction = DbContext.Find<Transaction>(stopTransactionRequest.TransactionId);
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "StopTransaction => Exception reading transaction: transactionId={0} / chargepoint={1}", stopTransactionRequest.TransactionId, ChargePointStatus?.Id);
                    errorCode = ErrorCodes.InternalError;
                }

                if (transaction != null)
                {
                    // Transaction found => check charge tag (the start tag and the car itself can also stop the transaction)

                    if (string.IsNullOrWhiteSpace(idTag))
                    {
                        // no RFID-Tag => accept stop request (can happen when the car stops the charging process)
                        stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                        Logger.LogInformation("StopTransaction => no charge tag => Status: {0}", stopTransactionResponse.IdTagInfo.Status);
                    }
                    else
                    {
                        bool? externalAuthResult = null;
                        try
                        {
                            // First step: call external authorizations
                            externalAuthResult = ocppMiddleware.ProcessExternalAuthorizations(AuthAction.StopTransaction, idTag, ChargePointStatus.Id, transaction?.ConnectorId, transaction?.Uid, transaction?.StartTagId);
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "StopTransaction => Exception from external authorization: {0}", exp.Message);
                        }

                        // Do we have a result from external authorizations?
                        if (externalAuthResult.HasValue)
                        {
                            // Yes => use this as accepted or invalid
                            if (externalAuthResult.Value)
                            {
                                stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                            }
                            else
                            {
                                stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                            }
                            Logger.LogInformation("StopTransaction => Extension auth. : Charge tag='{0}' => Status: {1}", idTag, stopTransactionResponse.IdTagInfo.Status);
                        }
                        else
                        {
                            // No result from external authorization => check local RFID tokens
                            try
                            {
                                stopTransactionResponse.IdTagInfo.ExpiryDate = MaxExpiryDate;
                                chargeTag = DbContext.Find<ChargeTag>(idTag);
                                if (chargeTag != null)
                                {
                                    if (chargeTag.ExpiryDate.HasValue) stopTransactionResponse.IdTagInfo.ExpiryDate = chargeTag.ExpiryDate.Value;
                                    stopTransactionResponse.IdTagInfo.ParentIdTag = chargeTag.ParentTagId;
                                    if (chargeTag.Blocked.HasValue && chargeTag.Blocked.Value)
                                    {
                                        stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Blocked;
                                    }
                                    else if (chargeTag.ExpiryDate.HasValue && chargeTag.ExpiryDate.Value < DateTime.Now)
                                    {
                                        stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Expired;
                                    }
                                    else
                                    {
                                        stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                                    }
                                }
                                else
                                {
                                    stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                                }

                                Logger.LogInformation("StopTransaction => RFID-tag='{0}' => Status: {1}", idTag, stopTransactionResponse.IdTagInfo.Status);

                            }
                            catch (Exception exp)
                            {
                                Logger.LogError(exp, "StopTransaction => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                                stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                            }
                        }
                    }
                }
                else
                {
                    // Error unknown transaction id
                    Logger.LogError("StopTransaction => Unknown or not matching transaction: id={0} / chargepoint={1} / tag={2}", stopTransactionRequest.TransactionId, ChargePointStatus?.Id, idTag);
                    WriteMessageLog(ChargePointStatus?.Id, transaction?.ConnectorId, msgIn.Action, string.Format("UnknownTransaction:ID={0}/Meter={1}", stopTransactionRequest.TransactionId, stopTransactionRequest.MeterStop), errorCode);
                    errorCode = ErrorCodes.PropertyConstraintViolation;
                }


                // But...
                // The charge tag which has started the transaction should always be able to stop the transaction.
                // (The owner needs to release his car :-) and the car can always forcingly stop the transaction)
                // => if status!=accepted check if it was the starting tag
                if (stopTransactionResponse.IdTagInfo.Status != IdTagInfoStatus.Accepted &&
                    transaction != null && !string.IsNullOrEmpty(transaction.StartTagId) &&
                    transaction.StartTagId.Equals(idTag, StringComparison.InvariantCultureIgnoreCase)) 
                {
                    // Override => allow the StartTagId to also stop the transaction
                    Logger.LogInformation("StopTransaction => RFID-tag='{0}' NOT accepted => override to ALLOWED because it is the start tag", idTag);
                    stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                }
                
                // General authorization done. Now check the result and update the transaction
                if (stopTransactionResponse.IdTagInfo.Status == IdTagInfoStatus.Accepted)
                {
                    try
                    {
                        if (transaction != null &&
                            transaction.ChargePointId == ChargePointStatus.Id &&
                            !transaction.StopTime.HasValue)
                        {
                            if (transaction.ConnectorId > 0)
                            {
                                // Update meter value in db connector status 
                                UpdateConnectorStatus(transaction.ConnectorId, null, null, (double)stopTransactionRequest.MeterStop / 1000, stopTransactionRequest.Timestamp);
                            }

                            // check current tag against start tag
                            bool valid = true;
                            if (!transaction.StartTagId.Equals(idTag, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // tags are different => same group?
                                ChargeTag startTag = DbContext.Find<ChargeTag>(transaction.StartTagId);
                                if (startTag != null)
                                {
                                    if (!string.Equals(startTag.ParentTagId, stopTransactionResponse.IdTagInfo.ParentIdTag, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        Logger.LogInformation("StopTransaction => Start-Tag ('{0}') and End-Tag ('{1}') do not match: Invalid!", transaction.StartTagId, idTag);
                                        stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                                        valid = false;
                                    }
                                    else
                                    {
                                        Logger.LogInformation("StopTransaction => Different RFID-Tags but matching group ('{0}')", stopTransactionResponse.IdTagInfo.ParentIdTag);
                                    }
                                }
                                else
                                {
                                    Logger.LogError("StopTransaction => Start-Tag not found: '{0}'", transaction.StartTagId);
                                    // assume "valid" and allow to end the transaction
                                }
                            }

                            if (valid)
                            {
                                transaction.StopTagId = idTag;
                                transaction.MeterStop =  (double)stopTransactionRequest.MeterStop / 1000; // Meter value here is always Wh
                                transaction.StopReason = stopTransactionRequest.Reason.ToString();
                                transaction.StopTime = stopTransactionRequest.Timestamp.UtcDateTime;
                                DbContext.SaveChanges();
                            }
                        }
                        else
                        {
                            Logger.LogError("StopTransaction => Unknown or not matching transaction: id={0} / chargepoint={1} / tag={2}", stopTransactionRequest.TransactionId, ChargePointStatus?.Id, idTag);
                            WriteMessageLog(ChargePointStatus?.Id, transaction?.ConnectorId, msgIn.Action, string.Format("UnknownTransaction:ID={0}/Meter={1}", stopTransactionRequest.TransactionId, stopTransactionRequest.MeterStop), errorCode);
                            errorCode = ErrorCodes.PropertyConstraintViolation;
                        }
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "StopTransaction => Exception writing transaction: chargepoint={0} / tag={1}", ChargePointStatus?.Id, idTag);
                        errorCode = ErrorCodes.InternalError;
                    }
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(stopTransactionResponse);
                Logger.LogTrace("StopTransaction => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "StopTransaction => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, stopTransactionResponse.IdTagInfo?.Status.ToString(), errorCode);
            return errorCode;
        }
    }
}
