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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP20;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleTransactionEvent(OCPPMessage msgIn, OCPPMessage msgOut, OCPPMiddleware ocppMiddleware)
        {
            string errorCode = null;
            TransactionEventResponse transactionEventResponse = new TransactionEventResponse();
            transactionEventResponse.IdTokenInfo = new IdTokenInfoType();
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

                        transactionEventResponse.IdTokenInfo = InternalAuthorize(idTag, ocppMiddleware, connectorId, AuthAction.StartTransaction, transactionEventRequest.TransactionInfo.TransactionId, string.Empty, denyConcurrentTx);

                        Logger.LogInformation("StartTransaction => Charge tag='{0}' => Status: {1}", idTag, transactionEventResponse.IdTokenInfo.Status);

                        if (transactionEventResponse.IdTokenInfo.Status == AuthorizationStatusEnumType.Accepted)
                        {
                            UpdateConnectorStatus(connectorId, ConnectorStatusEnum.Occupied.ToString(), meterTime, null, null);

                            try
                            {
                                Logger.LogInformation("StartTransaction => Meter='{0}' (kWh)", meterKWH);

                                Transaction transaction = new Transaction();
                                transaction.Uid = transactionEventRequest.TransactionInfo.TransactionId;
                                transaction.ChargePointId = ChargePointStatus?.Id;
                                transaction.ConnectorId = connectorId;
                                transaction.StartTagId = idTag;
                                transaction.StartTime = transactionEventRequest.Timestamp.UtcDateTime;
                                transaction.MeterStart = meterKWH;
                                transaction.StartResult = transactionEventRequest.TriggerReason.ToString();
                                DbContext.Add<Transaction>(transaction);

                                DbContext.SaveChanges();
                            }
                            catch (Exception exp)
                            {
                                Logger.LogError(exp, "StartTransaction => Exception writing transaction: chargepoint={0} / tag={1}", ChargePointStatus?.Id, idTag);
                                errorCode = ErrorCodes.InternalError;
                            }
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "StartTransaction => Exception: {0}", exp.Message);
                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }

                    msgLogText = $"StartTx => {transactionEventResponse.IdTokenInfo?.Status} | {msgLogText}";
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Updated)
                {
                    try
                    {
                        #region Update Transaction
                        Transaction transaction = DbContext.Transactions
                            .Where(t => t.Uid == transactionEventRequest.TransactionInfo.TransactionId)
                            .OrderByDescending(t => t.TransactionId)
                            .FirstOrDefault();

                        if (transaction != null &&
                            transaction.ChargePointId == ChargePointStatus.Id &&
                            !transaction.StopTime.HasValue)
                        {
                            // write current meter value in "stop" value
                            if (meterKWH >= 0)
                            {
                                Logger.LogInformation("UpdateTransaction => Meter='{0}' (kWh)", meterKWH);
                                transaction.MeterStop = meterKWH;
                                DbContext.SaveChanges();
                            }
                        }
                        else
                        {
                            Logger.LogError("UpdateTransaction => Unknown or not matching transaction: uid='{0}' / chargepoint='{1}' / tag={2}", transactionEventRequest.TransactionInfo?.TransactionId, ChargePointStatus?.Id, idTag);
                            WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, string.Format("UnknownTransaction:UID={0}/Meter={1}", transactionEventRequest.TransactionInfo?.TransactionId, GetMeterValue(transactionEventRequest.MeterValue)), errorCode);
                            errorCode = ErrorCodes.PropertyConstraintViolation;
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "UpdateTransaction => Exception: {0}", exp.Message);
                    }

                    msgLogText = $"UpdateTx => {msgLogText}";
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Ended)
                {
                    try
                    {
                        #region End Transaction

                        Transaction transaction = DbContext.Transactions
                            .Where(t => t.Uid == transactionEventRequest.TransactionInfo.TransactionId)
                            .OrderByDescending(t => t.TransactionId)
                            .FirstOrDefault();

                        if (transaction != null &&
                            transaction.ChargePointId == ChargePointStatus.Id &&
                            !transaction.StopTime.HasValue)
                        {
                            if (string.IsNullOrWhiteSpace(idTag))
                            {
                                // no RFID-Tag => accept stop request (can happen when the car stops the charging process)

                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                Logger.LogInformation("EndTransaction => no charge tag => accepted");
                            }
                            else
                            {
                                transactionEventResponse.IdTokenInfo = InternalAuthorize(idTag, ocppMiddleware, connectorId, AuthAction.StopTransaction, transactionEventRequest.TransactionInfo.TransactionId, transaction.StartTagId, false);
                            }
                        }
                        else
                        {
                            // Error unknown transaction id
                            Logger.LogError("EndTransaction => Unknown or not matching transaction: id={0} / chargepoint={1} / tag={2}", transactionEventRequest.TransactionInfo.TransactionId, ChargePointStatus?.Id, idTag);
                            WriteMessageLog(ChargePointStatus?.Id, transaction?.ConnectorId, msgIn.Action, string.Format("UnknownTransaction:ID={0}/Meter={1}", transactionEventRequest.TransactionInfo.TransactionId, transactionEventRequest.MeterValue), errorCode);
                            errorCode = ErrorCodes.PropertyConstraintViolation;
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



                        // General authorization done. Now check the result and update the transaction
                        if (transactionEventResponse.IdTokenInfo.Status == AuthorizationStatusEnumType.Accepted)
                        {
                            try
                            {
                                if (transaction != null &&
                                    transaction.ChargePointId == ChargePointStatus.Id &&
                                    !transaction.StopTime.HasValue)
                                {
                                    // check current tag against start tag
                                    bool valid = true;
                                    if (!transaction.StartTagId.Equals(idTag, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // tags are different => same group?
                                        ChargeTag startTag = DbContext.Find<ChargeTag>(transaction.StartTagId);
                                        if (startTag != null)
                                        {
                                            if (!string.Equals(startTag.ParentTagId, transactionEventResponse.IdTokenInfo.GroupIdToken?.IdToken, StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                Logger.LogInformation("EndTransaction => Start-Tag ('{0}') and End-Tag ('{1}') do not match: Invalid!", transaction.StartTagId, idTag);
                                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                                                valid = false;
                                            }
                                            else
                                            {
                                                Logger.LogInformation("EndTransaction => Different RFID-Tags but matching group ('{0}')", transactionEventResponse.IdTokenInfo.GroupIdToken?.IdToken);
                                            }
                                        }
                                        else
                                        {
                                            Logger.LogError("EndTransaction => Start-Tag not found: '{0}'", transaction.StartTagId);
                                            // assume "valid" and allow to end the transaction
                                        }
                                    }

                                    if (valid)
                                    {
                                        // write current meter value in "stop" value
                                        Logger.LogInformation("EndTransaction => Meter='{0}' (kWh)", meterKWH);

                                        transaction.StopTime = transactionEventRequest.Timestamp.UtcDateTime;
                                        transaction.MeterStop = meterKWH;
                                        transaction.StopTagId = idTag;
                                        transaction.StopReason = transactionEventRequest.TriggerReason.ToString();
                                        DbContext.SaveChanges();
                                    }
                                }
                                else
                                {
                                    Logger.LogError("EndTransaction => Unknown or not matching transaction: uid='{0}' / chargepoint='{1}' / tag={2}", transactionEventRequest.TransactionInfo?.TransactionId, ChargePointStatus?.Id, idTag);
                                    WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, string.Format("UnknownTransaction:UID={0}/Meter={1}", transactionEventRequest.TransactionInfo?.TransactionId, GetMeterValue(transactionEventRequest.MeterValue)), errorCode);
                                    errorCode = ErrorCodes.PropertyConstraintViolation;
                                }
                            }
                            catch (Exception exp)
                            {
                                Logger.LogError(exp, "EndTransaction => Exception: {0}", exp.Message);
                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                            }
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
                             sampleValue.Measurand == MeasurandEnumType.Missing ||
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

        /// <summary>
        /// Authorization logic for reuseability
        /// </summary>
        internal IdTokenInfoType InternalAuthorize(string idTag, OCPPMiddleware ocppMiddleware, int connectorId, AuthAction authAction, string transactionUid, string transactionStartId, bool denyConcurrentTx)
        {
            IdTokenInfoType idTokenInfo = new IdTokenInfoType();

            bool? externalAuthResult = null;
            try
            {
                externalAuthResult = ocppMiddleware.ProcessExternalAuthorizations(authAction, idTag, ChargePointStatus.Id, connectorId, transactionUid, transactionStartId);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "InternalAuthorize => Exception from external authorization (Action={0}, Tag={1}): {2}", authAction, idTag, exp.Message);
            }

            if (externalAuthResult.HasValue)
            {
                if (externalAuthResult.Value)
                {
                    idTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                }
                else
                {
                    idTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                }
                Logger.LogInformation("InternalAuthorize => Extension auth. : Action={0}, Tag='{1}' => Status: {2}", authAction, idTag, idTokenInfo.Status);
            }
            else
            {
                try
                {
                    ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                    if (ct != null)
                    {
                        if (!string.IsNullOrWhiteSpace(ct.ParentTagId))
                        {
                            idTokenInfo.GroupIdToken = new IdTokenType();
                            idTokenInfo.GroupIdToken.IdToken = ct.ParentTagId;
                        }
                        if (ct.Blocked.HasValue && ct.Blocked.Value)
                        {
                            idTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                        }
                        else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                        {
                            idTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                        }
                        else
                        {
                            idTokenInfo.Status = AuthorizationStatusEnumType.Accepted;

                            if (denyConcurrentTx)
                            {
                                // Check that no open transaction with this idTag exists
                                Transaction tx = DbContext.Transactions
                                    .Where(t => !t.StopTime.HasValue && t.StartTagId == ct.TagId)
                                    .OrderByDescending(t => t.TransactionId)
                                    .FirstOrDefault();

                                if (tx != null)
                                {
                                    idTokenInfo.Status = AuthorizationStatusEnumType.ConcurrentTx;
                                }
                            }
                        }
                    }
                    else
                    {
                        idTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }
                    Logger.LogInformation("InternalAuthorize => DB-Auth : Action={0}, Tag='{1}' => Status: {2}", authAction, idTag, idTokenInfo.Status);
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "InternalAuthorize => Exception reading charge tag (action={0}, tag={1}): {2}", authAction, idTag, exp.Message);
                    idTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                }
            }

            return idTokenInfo;
        }
    }
}
