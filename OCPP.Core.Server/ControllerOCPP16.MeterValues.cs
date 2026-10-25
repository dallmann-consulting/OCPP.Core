/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-20251 dallmann consulting GmbH.
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


/*
 http://www.diva-portal.se/smash/get/diva2:838105/FULLTEXT01.pdf

    Measurand values                    Description
    Energy.Active.Import.Register       Energy imported by EV (Wh of kWh)
    Power.Active.Import                 Instantaneous active power imported by EV (W or kW)
    Current.Import                      Instantaneous current flow to EV (A)
    Voltage                             AC RMS supply voltage (V)
    Temperature                         Temperature reading inside the charge point 

 <cs:meterValuesRequest>
   <cs:connectorId>0</cs:connectorId>
   <cs:transactionId>170</cs:transactionId>
   <cs:values>
     <cs:timestamp>2014-12-03T10:52:59.410Z</cs:timestamp>
     <cs:value cs:measurand="Current.Import" cs:unit="Amp">41.384</cs:value>
     <cs:value cs:measurand="Voltage" cs:unit="Volt">226.0</cs:value>
     <cs:value cs:measurand="Power.Active.Import" cs:unit="W">7018</cs:value>
     <cs:value cs:measurand="Energy.Active.Import.Register" cs:unit="Wh">2662</cs:value>
     <cs:value cs:measurand="Temperature" cs:unit="Celsius">24</cs:value>
   </cs:values>
 </cs:meterValuesRequest>
 */

using System;
using System.Collections.Generic;
using System.Globalization;
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
        public string HandleMeterValues(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            MeterValuesResponse meterValuesResponse = new MeterValuesResponse();

            int connectorId = -1;
            string msgMeterValue = string.Empty;

            try
            {
                Logger.LogTrace("Processing meter values...");
                MeterValuesRequest meterValueRequest = DeserializeMessage<MeterValuesRequest>(msgIn);
                Logger.LogTrace("MeterValues => Message deserialized");

                connectorId = meterValueRequest.ConnectorId;

                if (ChargePointStatus != null)
                {
                    // Known charge station => process meter values
                    // selection of the values: see MeterValueEvaluator
                    List<MeterSample> samples = new List<MeterSample>();
                    foreach (MeterValue meterValue in meterValueRequest.MeterValue)
                    {
                        foreach (SampledValue sampleValue in meterValue.SampledValue)
                        {
                            if (double.TryParse(sampleValue.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                            {
                                samples.Add(new MeterSample(
                                    meterValue.Timestamp,
                                    MeterValueEvaluator.EnumValue(sampleValue.Measurand),
                                    MeterValueEvaluator.EnumValue(sampleValue.Phase),
                                    MeterValueEvaluator.EnumValue(sampleValue.Location),
                                    value,
                                    MeterValueEvaluator.EnumValue(sampleValue.Unit)));
                            }
                            else
                            {
                                // e.g. signed data (Format = SignedData)
                                Logger.LogError("MeterValues => invalid value '{0}' (Measurand={1} / Format={2} / Unit={3})", sampleValue.Value, sampleValue.Measurand, sampleValue.Format, sampleValue.Unit);
                            }
                        }
                    }
                    MeterReading reading = MeterValueEvaluator.Evaluate(samples, Logger);
                    double meterKWH = reading.MeterKWH;
                    double currentChargeKW = reading.CurrentChargeKW;
                    double stateOfCharge = reading.StateOfCharge;
                    DateTimeOffset meterTime = reading.MeterTime ?? DateTime.UtcNow;

                    // write charging/meter data in chargepoint status
                    if (connectorId > 0)
                    {
                        msgMeterValue = $"Meter (kWh): {meterKWH} | Charge (kW): {currentChargeKW} | SoC (%): {stateOfCharge}";

                        if (meterKWH >= 0)
                        {
                            UpdateConnectorStatus(connectorId, null, null, meterKWH, meterTime);
                            UpdateMemoryConnectorStatus(connectorId, meterKWH, meterTime, currentChargeKW, stateOfCharge);
                        }
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
                errorCode = IsMessageFormatError(exp) ? ErrorCodes.FormationViolation : ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, connectorId, msgIn.Action, msgMeterValue, errorCode);
            return errorCode;
        }
    }
}
