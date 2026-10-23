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
                        stopTransactionResponse.IdTagInfo = InternalAuthorize(idTag, ocppMiddleware, transaction.ConnectorId, AuthAction.StopTransaction, transaction?.Uid, transaction?.StartTagId, false);
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
                

                // Authorization done (the response contains the real status of the tag => authorization cache of the charge point).
                // The transaction has already been stopped at the charge point and is always closed:
                // the charge point decides whether a tag may stop (same tag or same group, see Authorize/parentIdTag).
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
                            UpdateMemoryConnectorStatus(transaction.ConnectorId, (double)stopTransactionRequest.MeterStop / 1000, stopTransactionRequest.Timestamp, null, null);
                        }

                        // stop tag = start tag or same group? (only logged)
                        CheckStopTag(transaction, idTag, stopTransactionResponse.IdTagInfo.ParentIdTag);

                        transaction.StopTagId = idTag;
                        transaction.MeterStop =  (double)stopTransactionRequest.MeterStop / 1000; // Meter value here is always Wh
                        transaction.StopReason = stopTransactionRequest.Reason.ToString();
                        transaction.StopTime = stopTransactionRequest.Timestamp.UtcDateTime;
                        DbContext.SaveChanges();
                    }
                    else if (transaction != null)
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
