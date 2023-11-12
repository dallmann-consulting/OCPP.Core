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
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleNotifyEVChargingSchedule(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;

            Logger.LogTrace("Processing NotifyEVChargingSchedule...");
            NotifyEVChargingScheduleResponse notifyEVChargingScheduleResponse = new NotifyEVChargingScheduleResponse();
            notifyEVChargingScheduleResponse.CustomData = new CustomDataType();
            notifyEVChargingScheduleResponse.CustomData.VendorId = VendorId;

            StringBuilder periods = new StringBuilder();
            int connectorId = 0;

            try
            {
                NotifyEVChargingScheduleRequest notifyEVChargingScheduleRequest = DeserializeMessage<NotifyEVChargingScheduleRequest>(msgIn);
                Logger.LogTrace("NotifyEVChargingSchedule => Message deserialized");


                if (ChargePointStatus != null)
                {
                    // Known charge station
                    if (notifyEVChargingScheduleRequest.ChargingSchedule != null)
                    {
                        if (notifyEVChargingScheduleRequest.ChargingSchedule?.ChargingSchedulePeriod != null)
                        {
                            // Concat all periods and write them in messag log...

                            DateTimeOffset timeBase = notifyEVChargingScheduleRequest.TimeBase;
                            foreach (ChargingSchedulePeriodType period in notifyEVChargingScheduleRequest.ChargingSchedule?.ChargingSchedulePeriod)
                            {
                                if (periods.Length > 0)
                                {
                                    periods.Append(" | ");
                                }

                                DateTimeOffset time = timeBase.AddSeconds(period.StartPeriod);
                                periods.Append(string.Format("{0}: {1}{2}", time.ToString("O"), period.Limit, notifyEVChargingScheduleRequest.ChargingSchedule.ChargingRateUnit.ToString()));

                                if (period.NumberPhases > 0)
                                {
                                    periods.Append(string.Format(" ({0} Phases)", period.NumberPhases));
                                }
                            }
                        }
                    }
                    connectorId = notifyEVChargingScheduleRequest.EvseId;
                    Logger.LogInformation("NotifyEVChargingSchedule => {0}", periods.ToString());
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(notifyEVChargingScheduleResponse);
                Logger.LogTrace("NotifyEVChargingSchedule => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "NotifyEVChargingSchedule => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, connectorId, msgIn.Action, periods.ToString(), errorCode);
            return errorCode;
        }
    }
}
