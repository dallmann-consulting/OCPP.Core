/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP21;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP21
    {
        public string HandleTransactionEvent(OCPPMessage msgIn, OCPPMessage msgOut, OCPPMiddleware ocppMiddleware)
        {
            string errorCode = null;
            TransactionEventResponse transactionEventResponse = new TransactionEventResponse();
            transactionEventResponse.CustomData = new CustomDataType();
            transactionEventResponse.CustomData.VendorId = VendorId;

            int connectorId = 0;
            string msgLogText = string.Empty;

            try
            {
                Logger.LogTrace("TransactionEvent => Processing transactionEvent request...");
                TransactionEventRequest transactionEventRequest = DeserializeMessage<TransactionEventRequest>(msgIn);
                Logger.LogTrace("TransactionEvent => Message deserialized");

                string idTag = CleanChargeTagId(transactionEventRequest.IdToken?.IdToken, Logger);
                connectorId = (transactionEventRequest.Evse != null) ? 
                                        transactionEventRequest.Evse.Id : 0;

                //  Extract meter values with correct scale
                double currentChargeKW = -1;
                double meterKWH = -1;
                DateTimeOffset? meterTime = null;
                double stateOfCharge = -1;
                if (transactionEventRequest.MeterValue != null)
                {
                    GetMeterValues(transactionEventRequest.MeterValue, out meterKWH, out currentChargeKW, out stateOfCharge, out meterTime);
                    msgLogText = $"Meter (kWh): {meterKWH}";
                    if (currentChargeKW >= 0) msgLogText += $" | Charge (kW): {currentChargeKW}";
                    if (stateOfCharge >= 0) msgLogText += $" | SoC (%): {stateOfCharge}";
                }

                // If msg contains no time stamp => use current time
                if (!meterTime.HasValue) meterTime = DateTime.UtcNow;

                if (connectorId > 0 && meterKWH >= 0)
                {
                    UpdateConnectorStatus(connectorId, null, null, meterKWH, meterTime);
                    UpdateMemoryConnectorStatus(connectorId, meterKWH, meterTime.Value, currentChargeKW, stateOfCharge);
                }

                if (transactionEventRequest.EventType == TransactionEventEnumType.Started)
                {
                    try
                    {
                        #region Start Transaction
                        bool denyConcurrentTx = Configuration.GetValue<bool>("DenyConcurrentTx", false);

                        // A transaction can start without token (e.g. cable plugged in first => token follows in an "Updated" event)
                        string startResult = null;
                        if (!string.IsNullOrWhiteSpace(idTag))
                        {
                            transactionEventResponse.IdTokenInfo = new IdTokenInfoType();

                            bool? externalAuthResult = null;
                            try
                            {
                                externalAuthResult = ocppMiddleware.ProcessExternalAuthorizations(AuthAction.StartTransaction, idTag, ChargePointStatus.Id, connectorId, string.Empty, string.Empty);
                            }
                            catch (Exception exp)
                            {
                                Logger.LogError(exp, "StartTransaction => Exception from external authorization: {0}", exp.Message);
                            }

                            if (externalAuthResult.HasValue)
                            {
                                if (externalAuthResult.Value)
                                {
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                }
                                else
                                {
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                                }
                                Logger.LogInformation("StartTransaction => Extension auth. : Charge tag='{0}' => Status: {1}", idTag, transactionEventResponse.IdTokenInfo.Status);
                            }
                            else
                            {
                                ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                                if (ct != null)
                                {
                                    if (ct.Blocked.HasValue && ct.Blocked.Value)
                                    {
                                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                                    }
                                    else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                                    {
                                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                                    }
                                    else
                                    {
                                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                    }
                                }
                                else
                                {
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Unknown;
                                }
                            }

                            if (transactionEventResponse.IdTokenInfo.Status == AuthorizationStatusEnumType.Accepted &&
                                denyConcurrentTx)
                            {
                                // Check that no open transaction with this idTag exists
                                Transaction tx = DbContext.Transactions
                                    .Where(t => !t.StopTime.HasValue && t.StartTagId == idTag)
                                    .OrderByDescending(t => t.TransactionId)
                                    .FirstOrDefault();

                                if (tx != null)
                                {
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.ConcurrentTx;
                                }
                            }
                            startResult = transactionEventResponse.IdTokenInfo.Status.ToString();
                        }
                        else
                        {
                            // idTokenInfo is only allowed in the response when the request contains an idToken
                            transactionEventResponse.IdTokenInfo = null;
                        }

                        Logger.LogInformation("StartTransaction => Charge tag='{0}' => Status: {1}", idTag, startResult ?? "(no token)");

                        // The transaction has already started at the charging station
                        // => always record it. The authorization result is stored in "StartResult" and returned to the charging station.
                        UpdateConnectorStatus(connectorId, ConnectorStatusEnum.Occupied.ToString(), meterTime, null, null);

                        try
                        {
                            Logger.LogInformation("StartTransaction => Meter='{0}' (kWh)", meterKWH);

                            bool exists = DbContext.Transactions.Any(t => t.Uid == transactionEventRequest.TransactionInfo.TransactionId && t.ChargePointId == ChargePointStatus.Id);
                            if (exists)
                            {
                                // repeated message (e.g. response got lost) => don't create the transaction twice
                                Logger.LogWarning("StartTransaction => Transaction '{0}' already exists => ignored", transactionEventRequest.TransactionInfo.TransactionId);
                            }
                            else
                            {
                                CloseSupersededTransactions(connectorId, transactionEventRequest.Timestamp.UtcDateTime, meterKWH);

                                Transaction transaction = new Transaction();
                                transaction.Uid = transactionEventRequest.TransactionInfo.TransactionId;
                                transaction.ChargePointId = ChargePointStatus?.Id;
                                transaction.ConnectorId = connectorId;
                                transaction.StartTagId = string.IsNullOrWhiteSpace(idTag) ? null : idTag;
                                transaction.StartTime = transactionEventRequest.Timestamp.UtcDateTime;
                                transaction.MeterStart = meterKWH;
                                transaction.StartResult = startResult;
                                DbContext.Add<Transaction>(transaction);

                                DbContext.SaveChanges();
                            }
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "StartTransaction => Exception writing transaction: chargepoint={0} / tag={1}", ChargePointStatus?.Id, idTag);
                            errorCode = ErrorCodes.InternalError;
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "StartTransaction => Exception: {0}", exp.Message);
                        if (transactionEventResponse.IdTokenInfo != null)
                        {
                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                        }
                    }

                    msgLogText = $"StartTx => {transactionEventResponse.IdTokenInfo?.Status} | {msgLogText}";
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Updated)
                {
                    try
                    {
                        #region Update Transaction
                        Transaction transaction = FindTransactionByUid(transactionEventRequest.TransactionInfo.TransactionId);

                        if (transaction == null)
                        {
                            // unknown transaction (e.g. the "Started" message got lost) => create it from this event
                            // (start time and meter of this event => a small part of the energy may be missing)
                            Logger.LogWarning("UpdateTransaction => Unknown transaction uid='{0}' / chargepoint='{1}' => transaction is created from this event", transactionEventRequest.TransactionInfo.TransactionId, ChargePointStatus?.Id);
                            CloseSupersededTransactions(connectorId, transactionEventRequest.Timestamp.UtcDateTime, meterKWH);

                            transaction = new Transaction();
                            transaction.Uid = transactionEventRequest.TransactionInfo.TransactionId;
                            transaction.ChargePointId = ChargePointStatus.Id;
                            transaction.ConnectorId = connectorId;
                            transaction.StartTime = transactionEventRequest.Timestamp.UtcDateTime;
                            transaction.MeterStart = meterKWH;
                            DbContext.Add<Transaction>(transaction);
                            DbContext.SaveChanges();
                        }

                        if (transaction.StopTime.HasValue)
                        {
                            // transaction already ended => nothing to update (repeating doesn't help => answer positively)
                            Logger.LogInformation("UpdateTransaction => Transaction uid='{0}' is already ended => ignored", transaction.Uid);
                            transactionEventResponse.IdTokenInfo = string.IsNullOrWhiteSpace(idTag) ? null : InternalAuthorize(idTag, ocppMiddleware);
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(idTag))
                            {
                                // Token in "Updated" (e.g. cable plugged in first, then authorized) => authorize and answer with idTokenInfo
                                transactionEventResponse.IdTokenInfo = InternalAuthorize(idTag, ocppMiddleware);
                                if (string.IsNullOrEmpty(transaction.StartTagId))
                                {
                                    // transaction started without token => store the token as start tag
                                    Logger.LogInformation("UpdateTransaction => Start tag='{0}' => Status: {1}", idTag, transactionEventResponse.IdTokenInfo.Status);
                                    transaction.StartTagId = idTag;
                                    transaction.StartResult = transactionEventResponse.IdTokenInfo.Status.ToString();
                                    DbContext.SaveChanges();
                                }
                            }
                            else
                            {
                                // idTokenInfo is only allowed in the response when the request contains an idToken
                                transactionEventResponse.IdTokenInfo = null;
                            }

                            // write current meter value in "stop" value
                            if (meterKWH >= 0)
                            {
                                Logger.LogInformation("UpdateTransaction => Meter='{0}' (kWh)", meterKWH);
                                transaction.MeterStop = meterKWH;
                                DbContext.SaveChanges();
                            }
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        // e.g. database error => charging station should repeat the message
                        Logger.LogError(exp, "UpdateTransaction => Exception: {0}", exp.Message);
                        errorCode = ErrorCodes.InternalError;
                    }

                    msgLogText = $"UpdateTx => {msgLogText}";
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Ended)
                {
                    try
                    {
                        #region End Transaction
                        transactionEventResponse.IdTokenInfo = new IdTokenInfoType();

                        Transaction transaction = FindTransactionByUid(transactionEventRequest.TransactionInfo.TransactionId);

                        // authorization of the stop tag (also for unknown or ended transactions => real status in the response)
                        if (string.IsNullOrWhiteSpace(idTag))
                        {
                            // no RFID-Tag => accept stop request (can happen when the car stops the charging process)
                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                            Logger.LogInformation("EndTransaction => no charge tag => accepted");
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
                                Logger.LogError(exp, "EndTransaction => Exception from external authorization: {0}", exp.Message);
                            }

                            // Do we have a result from external authorizations?
                            if (externalAuthResult.HasValue)
                            {
                                // Yes => use this as accepted or invalid
                                if (externalAuthResult.Value)
                                {
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                }
                                else
                                {
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                                }
                                Logger.LogInformation("EndTransaction => Extension auth. : Charge tag='{0}' => Status: {1}", idTag, transactionEventResponse.IdTokenInfo.Status);
                            }
                            else
                            {
                                // No result from external authorization => check local RFID tokens
                                try
                                {
                                    ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                                    if (ct != null)
                                    {
                                        if (!string.IsNullOrWhiteSpace(ct.ParentTagId))
                                        {
                                            transactionEventResponse.IdTokenInfo.GroupIdToken = new IdTokenType();
                                            transactionEventResponse.IdTokenInfo.GroupIdToken.IdToken = ct.ParentTagId;
                                            // "type" is required in OCPP 2.1 (string) => without it the response can't be serialized
                                            transactionEventResponse.IdTokenInfo.GroupIdToken.Type = IdTokenEnumStringType.Central;
                                        }

                                        if (ct.Blocked.HasValue && ct.Blocked.Value)
                                        {
                                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                                        }
                                        else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                                        {
                                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                                        }
                                        else
                                        {
                                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                        }
                                    }
                                    else
                                    {
                                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Unknown;
                                    }

                                    Logger.LogInformation("EndTransaction => RFID-tag='{0}' => Status: {1}", idTag, transactionEventResponse.IdTokenInfo.Status);
                                }
                                catch (Exception exp)
                                {
                                    Logger.LogError(exp, "EndTransaction => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                                }
                            }
                        }


                        // But...
                        // The charge tag which has started the transaction should always be able to stop the transaction.
                        // (The owner needs to release his car :-) and the car can always forcingly stop the transaction)
                        // => if status!=accepted check if it was the starting tag
                        if (transactionEventResponse.IdTokenInfo.Status != AuthorizationStatusEnumType.Accepted &&
                            transaction != null && !string.IsNullOrEmpty(transaction.StartTagId) &&
                            transaction.StartTagId.Equals(idTag, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Override => allow the StartTagId to also stop the transaction
                            Logger.LogInformation("EndTransaction => RFID-tag='{0}' NOT accepted => override to ALLOWED because it is the start tag", idTag);
                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                        }



                        // Authorization done (the response contains the real status of the tag => authorization cache of the charging station).
                        // The transaction has already been stopped at the charging station and is always closed:
                        // the charging station decides whether a tag may stop (same tag or same group, see Authorize/groupIdToken).
                        // Error responses only for errors that a repetition of the message can solve (e.g. database errors),
                        // otherwise the charging station repeats the message and finally discards it.
                        try
                        {
                            if (transaction == null)
                            {
                                // unknown transaction => repeating doesn't help => answer positively and log the data
                                Logger.LogWarning("EndTransaction => Unknown transaction => ignored: uid='{0}' / chargepoint='{1}' / evse={2} / tag={3} / meter={4} / timestamp={5} / reason={6}",
                                    transactionEventRequest.TransactionInfo.TransactionId, ChargePointStatus?.Id, connectorId, idTag, meterKWH, transactionEventRequest.Timestamp, transactionEventRequest.TriggerReason);
                                WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, string.Format("UnknownTransaction:UID={0}/Meter={1}", transactionEventRequest.TransactionInfo.TransactionId, meterKWH), null);
                            }
                            else if (transaction.StopTime.HasValue && transaction.StopReason != TransactionStatus.StopReasonSuperseded)
                            {
                                // already ended (repeated message, e.g. the response got lost) => nothing to do
                                Logger.LogInformation("EndTransaction => Transaction uid='{0}' is already ended => repeated message ignored", transaction.Uid);
                            }
                            else
                            {
                                if (transaction.StopTime.HasValue)
                                {
                                    // closed by the server when the next transaction started (message was late) => take over the real values
                                    Logger.LogInformation("EndTransaction => Transaction uid='{0}' was closed automatically => real stop values are taken over", transaction.Uid);
                                }

                                // stop tag = start tag or same group? (only logged)
                                CheckStopTag(transaction, idTag, transactionEventResponse.IdTokenInfo.GroupIdToken?.IdToken);

                                // write current meter value in "stop" value
                                Logger.LogInformation("EndTransaction => Meter='{0}' (kWh)", meterKWH);

                                transaction.StopTime = transactionEventRequest.Timestamp.UtcDateTime;
                                if (meterKWH >= 0)
                                {
                                    // without meter value in the message => keep the last value
                                    transaction.MeterStop = meterKWH;
                                }
                                transaction.StopTagId = idTag;
                                transaction.StopReason = transactionEventRequest.TriggerReason.ToString();
                                DbContext.SaveChanges();
                            }
                        }
                        catch (Exception exp)
                        {
                            // e.g. database error => charging station should repeat the message
                            Logger.LogError(exp, "EndTransaction => Exception: {0}", exp.Message);
                            errorCode = ErrorCodes.InternalError;
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "EndTransaction => Exception: {0}", exp.Message);
                        errorCode = ErrorCodes.InternalError;
                    }

                    msgLogText = $"EndTx => {transactionEventResponse.IdTokenInfo?.Status} | {msgLogText}";
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(transactionEventResponse);
                Logger.LogTrace("TransactionEvent => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "TransactionEvent => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, msgLogText, errorCode);
            return errorCode;
        }


        /// <summary>
        /// Extract main meter value from collection
        /// </summary>
        private double GetMeterValue(ICollection<MeterValueType> meterValues)
        {
            double currentChargeKW = -1;
            double meterKWH = -1;
            DateTimeOffset? meterTime = null;
            double stateOfCharge = -1;
            GetMeterValues(meterValues, out meterKWH, out currentChargeKW, out stateOfCharge, out meterTime);

            return meterKWH;
        }

        /// <summary>
        /// Extract different meter values from collection
        /// </summary>
        private void GetMeterValues(ICollection<MeterValueType> meterValues, out double meterKWH, out double currentChargeKW, out double stateOfCharge, out DateTimeOffset? meterTime)
        {
            currentChargeKW = -1;
            meterKWH = -1;
            meterTime = null;
            stateOfCharge = -1;

            foreach (MeterValueType meterValue in meterValues)
            {
                foreach (SampledValueType sampleValue in meterValue.SampledValue)
                {
                    Logger.LogTrace("GetMeterValues => Context={0} / SignedMeterValue={1} / Value={2} / Unit={3} / Location={4} / Measurand={5} / Phase={6}",
                        sampleValue.Context, sampleValue.SignedMeterValue, sampleValue.Value, sampleValue.UnitOfMeasure, sampleValue.Location, sampleValue.Measurand, sampleValue.Phase);

                    if (sampleValue.Measurand == MeasurandEnumType.Power_Active_Import)
                    {
                        // current charging power
                        currentChargeKW = sampleValue.Value;
                        if (sampleValue.UnitOfMeasure?.Unit == "W" ||
                            sampleValue.UnitOfMeasure?.Unit == "VA" ||
                            sampleValue.UnitOfMeasure?.Unit == "var" ||
                            sampleValue.UnitOfMeasure?.Unit == null ||
                            sampleValue.UnitOfMeasure == null)
                        {
                            Logger.LogTrace("GetMeterValues => Charging '{0:0.0}' W", currentChargeKW);
                            // convert W => kW
                            currentChargeKW = currentChargeKW / 1000;
                        }
                        else if (sampleValue.UnitOfMeasure?.Unit == "KW" ||
                                sampleValue.UnitOfMeasure?.Unit == "kVA" ||
                                sampleValue.UnitOfMeasure?.Unit == "kvar")
                        {
                            // already kW => OK
                            Logger.LogTrace("GetMeterValues => Charging '{0:0.0}' kW", currentChargeKW);
                        }
                        else
                        {
                            Logger.LogWarning("GetMeterValues => Charging: unexpected unit: '{0}' (Value={1})", sampleValue.UnitOfMeasure?.Unit, sampleValue.Value);
                        }
                    }
                    else if (sampleValue.Measurand == MeasurandEnumType.Energy_Active_Import_Register ||
                             sampleValue.Measurand == null)  // Spec: Default=Energy_Active_Import_Register
                    {
                        // charged amount of energy
                        meterKWH = sampleValue.Value;
                        if (sampleValue.UnitOfMeasure?.Unit == "Wh" ||
                            sampleValue.UnitOfMeasure?.Unit == "VAh" ||
                            sampleValue.UnitOfMeasure?.Unit == "varh" ||
                            (sampleValue.UnitOfMeasure == null || sampleValue.UnitOfMeasure.Unit == null))
                        {
                            Logger.LogTrace("GetMeterValues => Value: '{0:0.0}' Wh", meterKWH);
                            // convert Wh => kWh
                            meterKWH = meterKWH / 1000;
                        }
                        else if (sampleValue.UnitOfMeasure?.Unit == "kWh" ||
                                sampleValue.UnitOfMeasure?.Unit == "kVAh" ||
                                sampleValue.UnitOfMeasure?.Unit == "kvarh")
                        {
                            // already kWh => OK
                            Logger.LogTrace("GetMeterValues => Value: '{0:0.0}' kWh", meterKWH);
                        }
                        else
                        {
                            Logger.LogWarning("GetMeterValues => Value: unexpected unit: '{0}' (Value={1})", sampleValue.UnitOfMeasure?.Unit, sampleValue.Value);
                        }
                        meterTime = meterValue.Timestamp;
                    }
                    else if (sampleValue.Measurand == MeasurandEnumType.SoC)
                    {
                        // state of charge (battery status)
                        stateOfCharge = sampleValue.Value;
                        Logger.LogTrace("GetMeterValues => SoC: '{0:0.0}'%", stateOfCharge);
                    }
                }
            }
        }
    }
}
