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
        public string HandleNotifyChargingLimit(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;

            Logger.LogTrace("Processing NotifyChargingLimit...");
            NotifyChargingLimitResponse notifyChargingLimitResponse = new NotifyChargingLimitResponse();
            notifyChargingLimitResponse.CustomData = new CustomDataType();
            notifyChargingLimitResponse.CustomData.VendorId = VendorId;

            string source = null;
            StringBuilder periods = new StringBuilder();
            int connectorId = 0;

            try
            {
                NotifyChargingLimitRequest notifyChargingLimitRequest = DeserializeMessage<NotifyChargingLimitRequest>(msgIn);
                Logger.LogTrace("NotifyChargingLimit => Message deserialized");


                if (ChargePointStatus != null)
                {
                    // Known charge station
                    source = notifyChargingLimitRequest.ChargingLimit?.ChargingLimitSource.ToString();
                    if (notifyChargingLimitRequest.ChargingSchedule != null)
                    {
                        foreach (ChargingScheduleType schedule in notifyChargingLimitRequest.ChargingSchedule)
                        {
                            if (schedule.ChargingSchedulePeriod != null)
                            {
                                foreach (ChargingSchedulePeriodType period in schedule.ChargingSchedulePeriod)
                                {
                                    if (periods.Length > 0)
                                    {
                                        periods.Append(" | ");
                                    }

                                    periods.Append(string.Format("{0}s: {1}{2}", period.StartPeriod, period.Limit, schedule.ChargingRateUnit));

                                    if (period.NumberPhases > 0)
                                    {
                                        periods.Append(string.Format(" ({0} Phases)", period.NumberPhases));
                                    }
                                }
                            }
                        }
                    }
                    connectorId = notifyChargingLimitRequest.EvseId;
                    Logger.LogInformation("NotifyChargingLimit => {0}", periods);
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(notifyChargingLimitResponse);
                Logger.LogTrace("NotifyChargingLimit => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "NotifyChargingLimit => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, connectorId, msgIn.Action, source, errorCode);
            return errorCode;
        }
    }
}
