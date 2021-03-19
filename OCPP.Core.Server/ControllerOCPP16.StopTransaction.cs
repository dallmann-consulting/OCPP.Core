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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleStopTransaction(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            StopTransactionResponse stopTransactionResponse = new StopTransactionResponse();

            try
            {
                Logger.LogTrace("Processing stopTransaction request...");
                StopTransactionRequest stopTransactionRequest = JsonConvert.DeserializeObject<StopTransactionRequest>(msgIn.JsonPayload);
                Logger.LogTrace("StopTransaction => Message deserialized");

                string idTag = stopTransactionRequest.IdTag;

                try
                {
                    using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                    {
                        stopTransactionResponse.IdTagInfo = new IdTagInfo();

                        ChargeTag ct = dbContext.Find<ChargeTag>(idTag);
                        if (ct != null)
                        {
                            stopTransactionResponse.IdTagInfo.ExpiryDate = ct.ExpiryDate.HasValue ? ct.ExpiryDate.Value : new DateTime(2999, 12, 31);
                            stopTransactionResponse.IdTagInfo.ParentIdTag = ct.ParentTagId;
                            if (ct.Blocked.HasValue && ct.Blocked.Value)
                            {
                                stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Blocked;
                            }
                            else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
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

                        Logger.LogInformation("StopTransaction => Status: {0}", stopTransactionResponse.IdTagInfo.Status);

                        try
                        {
                            Transaction transaction = dbContext.Find<Transaction>(stopTransactionRequest.TransactionId);
                            if (transaction == null || 
                                transaction.ChargePointId != ChargePointStatus.Id || 
                                transaction.StopTime.HasValue)
                            {
                                // unknown transaction id or already stopped transaction
                                // => find latest transaction for the charge point and check if its open
                                Logger.LogWarning("StopTransaction => Unknown or closed transaction id={0}", transaction?.TransactionId);
                                transaction = dbContext.Transactions
                                    .Where(t => t.ChargePointId == ChargePointStatus.Id)
                                    .OrderByDescending(t => t.TransactionId)
                                    .FirstOrDefault();

                                if (transaction != null)
                                {
                                    Logger.LogTrace("StopTransaction => Last transaction id={0} / Start='{1}' / Stop='{2}'", transaction.TransactionId, transaction.StartTime.ToString("O"), transaction?.StopTime?.ToString("o"));
                                    if (transaction.StopTime.HasValue)
                                    {
                                        Logger.LogTrace("StopTransaction => Last transaction (id={0}) is already closed ", transaction.TransactionId);
                                        transaction = null;
                                    }
                                }
                                else
                                {
                                    Logger.LogTrace("StopTransaction => Found no transaction for charge point '{0}'", ChargePointStatus.Id);
                                }
                            }

                            if (transaction != null)
                            {
                                // check current tag against start tag
                                bool valid = true;
                                if (!string.Equals(transaction.StartTagId, idTag, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // tags are different => same group?
                                    ChargeTag startTag = dbContext.Find<ChargeTag>(transaction.StartTagId);
                                    if (startTag != null)
                                    {
                                        if (!string.Equals(startTag.ParentTagId, ct?.ParentTagId, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            Logger.LogInformation("StopTransaction => Start-Tag ('{0}') and End-Tag ('{1}') do not match: Invalid!", transaction.StartTagId, ct?.TagId);
                                            stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                                            valid = false;
                                        }
                                        else
                                        {
                                            Logger.LogInformation("StopTransaction => Different RFID-Tags but matching group ('{0}')", ct?.ParentTagId);
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
                                    transaction.MeterStop = stopTransactionRequest.MeterStop;
                                    transaction.StopReason = stopTransactionRequest.Reason.ToString();
                                    transaction.StopTime = stopTransactionRequest.Timestamp.UtcDateTime;
                                    dbContext.SaveChanges();
                                }
                            }
                            else
                            {
                                Logger.LogError("StopTransaction => Unknown transaction: id={0} / chargepoint={1} / tag={2}", stopTransactionRequest.TransactionId, ChargePointStatus?.Id, idTag);
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
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "StopTransaction => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                    stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
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
