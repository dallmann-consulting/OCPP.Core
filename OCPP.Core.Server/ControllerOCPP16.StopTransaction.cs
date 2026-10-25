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

                // Transaction of this charge point?
                bool ownTransaction = (transaction != null && transaction.ChargePointId == ChargePointStatus.Id);

                if (string.IsNullOrWhiteSpace(idTag))
                {
                    // no RFID-Tag => accept stop request (can happen when the car stops the charging process)
                    stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                    Logger.LogInformation("StopTransaction => no charge tag => Status: {0}", stopTransactionResponse.IdTagInfo.Status);
                }
                else
                {
                    stopTransactionResponse.IdTagInfo = InternalAuthorize(idTag, ocppMiddleware, ownTransaction ? transaction.ConnectorId : 0, AuthAction.StopTransaction, transaction?.Uid, transaction?.StartTagId, false);
                }

                // But...
                // The charge tag which has started the transaction should always be able to stop the transaction.
                // (The owner needs to release his car :-) and the car can always forcingly stop the transaction)
                // => if status!=accepted check if it was the starting tag
                if (stopTransactionResponse.IdTagInfo.Status != IdTagInfoStatus.Accepted &&
                    ownTransaction && !string.IsNullOrEmpty(transaction.StartTagId) &&
                    transaction.StartTagId.Equals(idTag, StringComparison.InvariantCultureIgnoreCase))
                {
                    // Override => allow the StartTagId to also stop the transaction
                    Logger.LogInformation("StopTransaction => RFID-tag='{0}' NOT accepted => override to ALLOWED because it is the start tag", idTag);
                    stopTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                }

                // Authorization done (the response contains the real status of the tag => authorization cache of the charge point).
                // The transaction has already been stopped at the charge point and is always closed:
                // the charge point decides whether a tag may stop (same tag or same group, see Authorize/parentIdTag).
                // Error responses only for errors that a repetition of the message can solve (e.g. database errors),
                // otherwise the charge point repeats the message and finally discards it.
                try
                {
                    if (errorCode != null)
                    {
                        // error reading the transaction => charge point should repeat the message
                    }
                    else if (!ownTransaction)
                    {
                        // unknown transaction (or of another charge point) => repeating doesn't help => answer positively and log the data
                        Logger.LogWarning("StopTransaction => Unknown transaction => ignored: id={0} / chargepoint={1} / tag={2} / meterStop={3} / timestamp={4} / reason={5}",
                            stopTransactionRequest.TransactionId, ChargePointStatus?.Id, idTag, stopTransactionRequest.MeterStop, stopTransactionRequest.Timestamp, stopTransactionRequest.Reason);
                        WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, string.Format("UnknownTransaction:ID={0}/Meter={1}", stopTransactionRequest.TransactionId, stopTransactionRequest.MeterStop), null);
                    }
                    else if (transaction.StopTime.HasValue && transaction.StopReason != TransactionStatus.StopReasonSuperseded)
                    {
                        // already stopped (repeated message, e.g. the response got lost) => nothing to do
                        Logger.LogInformation("StopTransaction => Transaction {0} is already stopped => repeated message ignored", transaction.TransactionId);
                    }
                    else
                    {
                        if (transaction.StopTime.HasValue)
                        {
                            // closed by the server when the next transaction started (stop message was late) => take over the real values
                            Logger.LogInformation("StopTransaction => Transaction {0} was closed automatically => real stop values are taken over", transaction.TransactionId);
                        }
                        else if (transaction.ConnectorId > 0)
                        {
                            // Update meter value in db connector status (not for a late stop => the connector has a newer transaction)
                            UpdateConnectorStatus(transaction.ConnectorId, null, null, (double)stopTransactionRequest.MeterStop / 1000, stopTransactionRequest.Timestamp);
                            UpdateMemoryConnectorStatus(transaction.ConnectorId, (double)stopTransactionRequest.MeterStop / 1000, stopTransactionRequest.Timestamp, null, null);
                        }

                        // stop tag = start tag or same group? (only logged)
                        CheckStopTag(transaction, idTag, stopTransactionResponse.IdTagInfo.ParentIdTag);

                        transaction.StopTagId = idTag;
                        // Meter value here is always Wh. Taken over unchanged (also if smaller than the start value => warning)
                        SetTransactionMeter(transaction, (double)stopTransactionRequest.MeterStop / 1000, isFinal: true, msgIn.Action);
                        transaction.StopReason = stopTransactionRequest.Reason.ToString();
                        transaction.StopTime = stopTransactionRequest.Timestamp.UtcDateTime;
                        DbContext.SaveChanges();
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
                errorCode = IsMessageFormatError(exp) ? ErrorCodes.FormationViolation : ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, stopTransactionResponse.IdTagInfo?.Status.ToString(), errorCode);
            return errorCode;
        }
    }
}
