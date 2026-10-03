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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_Api;
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleNotifyReport(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            string result = null;

            Logger.LogTrace("Processing NotifyReport...");
            try
            {
                NotifyReportRequest notifyReportRequest = DeserializeMessage<NotifyReportRequest>(msgIn);
                result = $"RequestId={notifyReportRequest.RequestId} / SeqNo={notifyReportRequest.SeqNo} / Tbc={notifyReportRequest.Tbc} / Items={notifyReportRequest.ReportData?.Count}";
                Logger.LogInformation("NotifyReport => {0}", result);

                if (ChargePointStatus.PendingReports.TryGetValue(notifyReportRequest.RequestId, out PendingReport pendingReport))
                {
                    pendingReport.AddPart(notifyReportRequest.SeqNo, notifyReportRequest.Tbc ?? false, ToApiVariableResults(notifyReportRequest.ReportData));
                }
                else
                {
                    // late part (after timeout) or report not requested by the API => answer anyway
                    Logger.LogWarning("NotifyReport => No pending report for RequestId={0}", notifyReportRequest.RequestId);
                }

                // Every NotifyReport must be answered (otherwise the chargepoint may stop sending further parts)
                msgOut.JsonPayload = JsonConvert.SerializeObject(new NotifyReportResponse());
                Logger.LogTrace("NotifyReport => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "NotifyReport => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            WriteMessageLog(ChargePointStatus.Id, null, msgIn.Action, result, errorCode);
            return errorCode;
        }

        /// <summary>
        /// Maps report data to API results (one result per variable attribute)
        /// </summary>
        private static List<ApiVariableResult> ToApiVariableResults(List<ReportDataType> reportData)
        {
            List<ApiVariableResult> results = new List<ApiVariableResult>();
            if (reportData == null) return results;

            foreach (ReportDataType data in reportData)
            {
                ApiComponent component = new ApiComponent()
                {
                    Name = data.Component?.Name,
                    Instance = data.Component?.Instance,
                    Evse = (data.Component?.Evse == null) ? null : new ApiEvse() { Id = data.Component.Evse.Id, ConnectorId = data.Component.Evse.ConnectorId }
                };
                ApiVariable variable = new ApiVariable() { Name = data.Variable?.Name, Instance = data.Variable?.Instance };
                VariableCharacteristicsType characteristics = data.VariableCharacteristics;

                foreach (VariableAttributeType attribute in data.VariableAttribute ?? new List<VariableAttributeType>())
                {
                    results.Add(new ApiVariableResult()
                    {
                        Component = component,
                        Variable = variable,
                        AttributeType = (attribute.Type ?? AttributeEnumType.Actual).ToString(),
                        Value = attribute.Value,
                        Status = ApiVariableStatus.Accepted,
                        // default is ReadWrite when omitted
                        Mutability = (attribute.Mutability ?? MutabilityEnumType.ReadWrite).ToString(),
                        DataType = characteristics?.DataType.ToString(),
                        Unit = characteristics?.Unit,
                        MinLimit = characteristics?.MinLimit,
                        MaxLimit = characteristics?.MaxLimit,
                        ValuesList = characteristics?.ValuesList
                    });
                }
            }
            return results;
        }
    }
}
