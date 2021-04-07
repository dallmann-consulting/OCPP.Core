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
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleMeterValues(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            MeterValuesResponse meterValuesResponse = new MeterValuesResponse();

            meterValuesResponse.CustomData = new CustomDataType();
            meterValuesResponse.CustomData.VendorId = VendorId;

            int? connectorId = null;

            try
            {
                Logger.LogTrace("Processing meter values...");
                MeterValuesRequest meterValueRequest = JsonConvert.DeserializeObject<MeterValuesRequest>(msgIn.JsonPayload);
                Logger.LogTrace("MeterValues => Message deserialized");

                connectorId = meterValueRequest.EvseId;

                if (ChargePointStatus != null)
                {
                    // Known charge station => process meter values
                    double currentChargeKW = -1;
                    double meterKWH = -1;
                    double stateOfCharge = -1;
                    foreach (MeterValueType meterValue in meterValueRequest.MeterValue)
                    {
                        foreach (SampledValueType sampleValue in meterValue.SampledValue)
                        {
                            Logger.LogTrace("MeterValues => Context={0} / SignedMeterValue={1} / Value={2} / Unit={3} / Location={4} / Measurand={5} / Phase={6}",
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
                                    Logger.LogTrace("MeterValues => Charging '{0:0.0}' W", currentChargeKW);
                                    // convert W => kW
                                    currentChargeKW = currentChargeKW / 1000;
                                }
                                else if (sampleValue.UnitOfMeasure?.Unit == "KW" ||
                                        sampleValue.UnitOfMeasure?.Unit == "kVA" ||
                                        sampleValue.UnitOfMeasure?.Unit == "kvar")
                                {
                                    // already kW => OK
                                    Logger.LogTrace("MeterValues => Charging '{0:0.0}' kW", currentChargeKW);
                                }
                                else
                                {
                                    Logger.LogWarning("MeterValues => Charging: unexpected unit: '{0}' (Value={1})", sampleValue.UnitOfMeasure?.Unit, sampleValue.Value);
                                }
                            }
                            else if (sampleValue.Measurand == MeasurandEnumType.Energy_Active_Import_Register)
                            {
                                // charged amount of energy
                                meterKWH = sampleValue.Value;
                                if (sampleValue.UnitOfMeasure?.Unit == "Wh" ||
                                    sampleValue.UnitOfMeasure?.Unit == "VAh" ||
                                    sampleValue.UnitOfMeasure?.Unit == "varh" ||
                                    (sampleValue.UnitOfMeasure == null || sampleValue.UnitOfMeasure.Unit == null))
                                {
                                    Logger.LogTrace("MeterValues => Value: '{0:0.0}' Wh", meterKWH);
                                    // convert Wh => kWh
                                    meterKWH = meterKWH / 1000;
                                }
                                else if (sampleValue.UnitOfMeasure?.Unit == "kWh" ||
                                        sampleValue.UnitOfMeasure?.Unit == "kVAh" ||
                                        sampleValue.UnitOfMeasure?.Unit == "kvarh")
                                {
                                    // already kWh => OK
                                    Logger.LogTrace("MeterValues => Value: '{0:0.0}' kWh", meterKWH);
                                }
                                else
                                {
                                    Logger.LogWarning("MeterValues => Value: unexpected unit: '{0}' (Value={1})", sampleValue.UnitOfMeasure?.Unit, sampleValue.Value);
                                }
                            }
                            else if (sampleValue.Measurand == MeasurandEnumType.SoC)
                            {
                                // state of charge (battery status)
                                stateOfCharge = sampleValue.Value;
                                Logger.LogTrace("MeterValues => SoC: '{0:0.0}'%", stateOfCharge);
                            }
                        }
                    }

                    // write charging/meter data in chargepoint status
                    ChargingData chargingData = null;
                    if (currentChargeKW >= 0 || meterKWH >= 0 || stateOfCharge >= 0)
                    {
                        chargingData = new ChargingData();
                        if (currentChargeKW >= 0) chargingData.ChargeRateKW = currentChargeKW;
                        if (meterKWH >= 0) chargingData.MeterKWH = meterKWH;
                        if (stateOfCharge >= 0) chargingData.SoC = stateOfCharge;
                    }
                    if (connectorId > 1)
                    {
                        // second connector (odr higher!?)
                        ChargePointStatus.ChargingDataEVSE2 = chargingData;
                    }
                    else
                    {
                        // first connector
                        ChargePointStatus.ChargingDataEVSE1 = chargingData;
                    }
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(meterValuesResponse);
                Logger.LogTrace("MeterValues => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MeterValues => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, connectorId, msgIn.Action, null, errorCode);
            return errorCode;
        }
    }
}
