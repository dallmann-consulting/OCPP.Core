/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
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
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleStartTransaction(OCPPMessage msgIn, OCPPMessage msgOut, OCPPMiddleware ocppMiddleware)
        {
            string errorCode = null;
            StartTransactionResponse startTransactionResponse = new StartTransactionResponse();

            int connectorId = -1;
            bool denyConcurrentTx = Configuration.GetValue<bool>("DenyConcurrentTx", false);

            try
            {
                Logger.LogTrace("Processing startTransaction request...");
                StartTransactionRequest startTransactionRequest = DeserializeMessage<StartTransactionRequest>(msgIn);
                Logger.LogTrace("StartTransaction => Message deserialized");

                string idTag = CleanChargeTagId(startTransactionRequest.IdTag, Logger);
                DateTime startTime = startTransactionRequest.Timestamp.UtcDateTime;
                double meterStart = (double)startTransactionRequest.MeterStart / 1000; // Meter value here is always Wh

                // Repeated message (e.g. the response got lost)? => identical connector, start time, meter and tag
                // (two real transactions never have the same start time and meter)
                Transaction existingTransaction = DbContext.Transactions
                    .Where(t => t.ChargePointId == ChargePointStatus.Id &&
                                t.ConnectorId == startTransactionRequest.ConnectorId &&
                                t.StartTime == startTime &&
                                t.MeterStart == meterStart &&
                                t.StartTagId == idTag)
                    .OrderByDescending(t => t.TransactionId)
                    .FirstOrDefault();

                // for a repeated message no check for concurrent transactions (the first message has created the open transaction)
                startTransactionResponse.IdTagInfo = InternalAuthorize(idTag, ocppMiddleware, startTransactionRequest.ConnectorId, AuthAction.StartTransaction, string.Empty, string.Empty, denyConcurrentTx && existingTransaction == null);

                if (connectorId > 0)
                {
                    // Update meter value in db connector status 
                    UpdateConnectorStatus(connectorId, ConnectorStatusEnum.Occupied.ToString(), startTransactionRequest.Timestamp, (double)startTransactionRequest.MeterStart / 1000, startTransactionRequest.Timestamp);
                    UpdateMemoryConnectorStatus(connectorId, (double)startTransactionRequest.MeterStart / 1000, startTransactionRequest.Timestamp, null, null);
                }

                // The transaction has already started at the charge point (e.g. authorized offline)
                // => always record it. The authorization result is stored in "StartResult" and returned to the charge point.
                try
                {
                    if (existingTransaction != null)
                    {
                        // repeated message => return the transaction ID again (the charge point uses it in StopTransaction)
                        Logger.LogInformation("StartTransaction => Repeated message for transaction {0} => no new transaction", existingTransaction.TransactionId);
                        startTransactionResponse.TransactionId = existingTransaction.TransactionId;
                    }
                    else
                    {
                        CloseSupersededTransactions(startTransactionRequest.ConnectorId, startTime, meterStart);

                        Transaction transaction = new Transaction();
                        transaction.ChargePointId = ChargePointStatus?.Id;
                        transaction.ConnectorId = startTransactionRequest.ConnectorId;
                        transaction.StartTagId = idTag;
                        transaction.StartTime = startTime;
                        transaction.MeterStart = meterStart;
                        transaction.StartResult = startTransactionResponse.IdTagInfo.Status.ToString();
                        DbContext.Add<Transaction>(transaction);
                        DbContext.SaveChanges();

                        // Return DB-ID as transaction ID
                        startTransactionResponse.TransactionId = transaction.TransactionId;
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "StartTransaction => Exception writing transaction: chargepoint={0} / tag={1}", ChargePointStatus?.Id, idTag);
                    errorCode = ErrorCodes.InternalError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(startTransactionResponse);
                Logger.LogTrace("StartTransaction => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "StartTransaction => Exception: {0}", exp.Message);
                errorCode = IsMessageFormatError(exp) ? ErrorCodes.FormationViolation : ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, startTransactionResponse.IdTagInfo?.Status.ToString(), errorCode);
            return errorCode;
        }
    }
}
