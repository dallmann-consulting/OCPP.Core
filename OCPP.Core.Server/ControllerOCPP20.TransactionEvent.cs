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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleTransactionEvent(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            TransactionEventResponse transactionEventResponse = new TransactionEventResponse();
            transactionEventResponse.CustomData = new CustomDataType();
            transactionEventResponse.CustomData.VendorId = VendorId;
            transactionEventResponse.IdTokenInfo = new IdTokenInfoType();

            int connectorId = 0;

            try
            {
                Logger.LogTrace("TransactionEvent => Processing transactionEvent request...");
                TransactionEventRequest transactionEventRequest = DeserializeMessage<TransactionEventRequest>(msgIn);
                Logger.LogTrace("TransactionEvent => Message deserialized");

                string idTag = CleanChargeTagId(transactionEventRequest.IdToken?.IdToken, Logger);
                ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                connectorId = (transactionEventRequest.Evse != null) ? transactionEventRequest.Evse.ConnectorId : 0;


                //  Extract meter values with correct scale
                double currentChargeKW = -1;
                double meterKWH = -1;
                DateTimeOffset? meterTime = null;
                double stateOfCharge = -1;
                GetMeterValues(transactionEventRequest.MeterValue, out meterKWH, out currentChargeKW, out stateOfCharge, out meterTime);

                if (connectorId > 0 && meterKWH >= 0)
                {
                    UpdateConnectorStatus(connectorId, null, null, meterKWH, meterTime);
                }

                if (transactionEventRequest.EventType == TransactionEventEnumType.Started)
                {
                    try
                    {
                        #region Start Transaction
                        bool denyConcurrentTx = Configuration.GetValue<bool>("DenyConcurrentTx", false);

                        if (string.IsNullOrWhiteSpace(idTag))
                        {
                            // no RFID-Tag => accept request
                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                            Logger.LogInformation("StartTransaction => no charge tag => accepted");
                        }
                        else
                        {
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

                                    if (denyConcurrentTx)
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
                                }
                            }
                            else
                            {
                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Unknown;
                            }

                            Logger.LogInformation("StartTransaction => Charge tag='{0}' => Status: {1}", idTag, transactionEventResponse.IdTokenInfo.Status);

                        }

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
                                transaction.StartTagId = ct.TagId;
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
                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Ended)
                {
                    try
                    {
                        #region End Transaction
                        ct = null;

                        if (string.IsNullOrWhiteSpace(idTag))
                        {
                            // no RFID-Tag => accept request
                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                            Logger.LogInformation("EndTransaction => no charge tag => accepted");
                        }
                        else
                        {
                            ct = DbContext.Find<ChargeTag>(idTag);
                            if (ct != null)
                            {
                                if (ct.Blocked.HasValue && ct.Blocked.Value)
                                {
                                    Logger.LogInformation("EndTransaction => Tag '{1}' blocked)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                                }
                                else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                                {
                                    Logger.LogInformation("EndTransaction => Tag '{1}' expired)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                                }
                                else
                                {
                                    Logger.LogInformation("EndTransaction => Tag '{1}' accepted)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                }
                            }
                            else
                            {
                                Logger.LogInformation("EndTransaction => Tag '{1}' unknown)", idTag);
                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Unknown;
                            }
                        }

                        Transaction transaction = DbContext.Transactions
                            .Where(t => t.Uid == transactionEventRequest.TransactionInfo.TransactionId)
                            .OrderByDescending(t => t.TransactionId)
                            .FirstOrDefault();

                        if (transaction != null &&
                            transaction.ChargePointId == ChargePointStatus.Id &&
                            !transaction.StopTime.HasValue)
                        {
                            // check current tag against start tag
                            bool valid = true;
                            if (!string.Equals(transaction.StartTagId, idTag, StringComparison.InvariantCultureIgnoreCase))
                            {
                                // tags are different => same group?
                                ChargeTag startTag = DbContext.Find<ChargeTag>(transaction.StartTagId);
                                if (startTag != null)
                                {
                                    if (!string.Equals(startTag.ParentTagId, ct?.ParentTagId, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        Logger.LogInformation("EndTransaction => Start-Tag ('{0}') and End-Tag ('{1}') do not match: Invalid!", transaction.StartTagId, ct?.TagId);
                                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                                        valid = false;
                                    }
                                    else
                                    {
                                        Logger.LogInformation("EndTransaction => Different charge tags but matching group ('{0}')", ct?.ParentTagId);
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
                                transaction.StopTagId = ct.TagId;
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
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "EndTransaction => Exception: {0}", exp.Message);
                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(transactionEventResponse);
                Logger.LogTrace("TransactionEvent => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "TransactionEvent => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, transactionEventResponse.IdTokenInfo.Status.ToString(), errorCode);
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
                             sampleValue.Measurand == MeasurandEnumType.Missing)  // Spec: Default=Energy_Active_Import_Register
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
