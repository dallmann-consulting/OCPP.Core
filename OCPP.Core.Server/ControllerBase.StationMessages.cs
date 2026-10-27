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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Validation;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    /// <summary>
    /// OCPP 2.x: messages of the charge point without own processing (no message classes).
    /// They are validated against their schema, answered as the specification requires and written into the EventLog.
    /// </summary>
    public partial class ControllerBase
    {
        /// <summary>
        /// Messages of the charge point that are processed by <see cref="HandleStationMessage"/>
        /// </summary>
        protected static readonly HashSet<string> StationMessageActions = new HashSet<string>
        {
            // reports of the charge point
            "SecurityEventNotification", "NotifyEvent",
            // functions the server doesn't offer (certificates, ISO 15118) => rejected
            "SignCertificate", "Get15118EVCertificate", "GetCertificateStatus", "NotifyEVChargingNeeds",
            // answers to requests the server never sends (or after a reservation) => accepted
            "ReservationStatusUpdate", "NotifyMonitoringReport", "ReportChargingProfiles",
            "NotifyCustomerInformation", "NotifyDisplayMessages", "PublishFirmwareStatusNotification"
        };

        // security events without a security problem (OCPP 2.0.1 part 2 appendix 1)
        private static readonly HashSet<string> RoutineSecurityEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FirmwareUpdated", "StartupOfTheDevice", "ResetOrReboot", "SettingSystemTime"
        };

        // max. length of the message content stored in the EventLog details
        private const int MaxDetailsLength = 2000;

        /// <summary>
        /// Processes a message of <see cref="StationMessageActions"/> and returns the error code (null = OK)
        /// </summary>
        protected string HandleStationMessage(OCPPMessage msgIn, OCPPMessage msgOut, string formatErrorCode, string internalErrorCode)
        {
            try
            {
                JObject request = ParseMessage(msgIn);
                JObject response = new JObject();

                switch (msgIn.Action)
                {
                    case "SecurityEventNotification":
                        WriteSecurityEvent(request);
                        break;
                    case "NotifyEvent":
                        WriteStationEvents(request);
                        break;

                    case "SignCertificate":
                        // A03.FR.12: the server can't process the CSR (no certificate authority)
                        response["status"] = "Rejected";
                        WriteFunctionNotSupported(msgIn, null, "Rejected", request["certificateType"]?.ToString());
                        break;
                    case "Get15118EVCertificate":
                        // ISO 15118 Plug & Charge isn't supported
                        response["status"] = "Failed";
                        response["exiResponse"] = string.Empty;
                        WriteFunctionNotSupported(msgIn, null, "Failed", request["action"]?.ToString());
                        break;
                    case "GetCertificateStatus":
                        // M06: no OCSP request possible
                        response["status"] = "Failed";
                        WriteFunctionNotSupported(msgIn, null, "Failed", null);
                        break;
                    case "NotifyEVChargingNeeds":
                        // K15.FR.04: the server can't provide a charging schedule (OCPP 2.1: "NoChargingProfile")
                        string status = ProtocolVersion == "21" ? "NoChargingProfile" : "Rejected";
                        response["status"] = status;
                        WriteFunctionNotSupported(msgIn, request["evseId"]?.Value<int?>(), status, null);
                        break;

                    default:
                        // answers to requests the server never sends => accepted, but noted
                        WriteEvent(null, EventCategory.Station, EventSeverity.Warning, EventCode.UnexpectedMessage,
                            new Dictionary<string, object> { ["action"] = msgIn.Action }, Truncate(msgIn.JsonPayload));
                        break;
                }

                msgOut.JsonPayload = response.ToString(Formatting.None);
                return null;
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "{0} => Exception: {1}", msgIn.Action, exp.Message);
                return IsMessageFormatError(exp) ? formatErrorCode : internalErrorCode;
            }
        }

        /// <summary>
        /// EventLog: message of the charge point that the server doesn't support (answered with a CALLERROR)
        /// => it's visible when such messages arrive
        /// </summary>
        protected void WriteUnsupportedMessage(OCPPMessage msgIn)
        {
            WriteEvent(null, EventCategory.Station, EventSeverity.Warning, EventCode.UnsupportedMessage,
                new Dictionary<string, object> { ["action"] = msgIn.Action }, Truncate(msgIn.JsonPayload));
        }

        /// <summary>
        /// Validates a message against the schema "[Action]Request" and returns its content
        /// </summary>
        private JObject ParseMessage(OCPPMessage msg)
        {
            if (Configuration.GetValue<bool>("ValidateMessages", false))
            {
                JsonSchema schema = GetSchema($"{msg.Action}Request");
                if (schema != null)
                {
                    ICollection<ValidationError> errors = schema.Validate(msg.JsonPayload);
                    if (errors.Count > 0)
                    {
                        foreach (ValidationError err in errors)
                        {
                            Logger.LogWarning("ParseMessage {0} => Validation error: {1}", msg.Action, err);
                        }
                        throw new FormatException("Message validation failed");
                    }
                }
            }
            return JObject.Parse(string.IsNullOrWhiteSpace(msg.JsonPayload) ? "{}" : msg.JsonPayload);
        }

        /// <summary>
        /// SecurityEventNotification => EventLog
        /// </summary>
        private void WriteSecurityEvent(JObject request)
        {
            string type = request["type"]?.ToString();
            string severity = RoutineSecurityEvents.Contains(type) ? EventSeverity.Info :
                              string.Equals(type, "TamperDetectionActivated", StringComparison.OrdinalIgnoreCase) ? EventSeverity.Error : EventSeverity.Warning;
            Logger.LogInformation("SecurityEventNotification => Chargepoint '{0}': {1} ({2})", ChargePointStatus?.Id, type, request["techInfo"]);

            Dictionary<string, object> parameters = new Dictionary<string, object> { ["type"] = type };
            string techInfo = request["techInfo"]?.ToString();
            if (!string.IsNullOrEmpty(techInfo)) parameters["techInfo"] = techInfo;
            WriteEvent(null, EventCategory.Station, severity, EventCode.SecurityEvent, parameters, request["timestamp"]?.ToString());
        }

        /// <summary>
        /// NotifyEvent => one EventLog entry per event of the charge point
        /// (not for "Periodic" monitors: regular values, no event)
        /// </summary>
        private void WriteStationEvents(JObject request)
        {
            if (request["eventData"] is not JArray eventData) return;

            foreach (JToken e in eventData)
            {
                string trigger = e["trigger"]?.ToString();
                if (trigger == "Periodic") continue;

                string component = NameWithInstance(e["component"]);
                string variable = NameWithInstance(e["variable"]);
                string value = e["actualValue"]?.ToString();
                bool cleared = e["cleared"]?.Value<bool>() == true;
                int? connectorId = e.SelectToken("component.evse.id")?.Value<int?>();

                // "Problem" (standardized variable of many components): true = error, false = solved
                string severity;
                if (string.Equals(e.SelectToken("variable.name")?.ToString(), "Problem", StringComparison.OrdinalIgnoreCase))
                {
                    severity = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) && !cleared ? EventSeverity.Error : EventSeverity.Info;
                }
                else
                {
                    // monitors (thresholds) => warning until cleared
                    severity = trigger == "Alerting" && !cleared ? EventSeverity.Warning : EventSeverity.Info;
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    ["component"] = component,
                    ["variable"] = variable,
                    ["value"] = value,
                    ["trigger"] = trigger
                };
                if (cleared) parameters["cleared"] = true;
                string techCode = e["techCode"]?.ToString();
                if (!string.IsNullOrEmpty(techCode)) parameters["techCode"] = techCode;
                WriteEvent(connectorId > 0 ? connectorId : null, EventCategory.Station, severity, EventCode.StationEvent, parameters, e["techInfo"]?.ToString());
            }
        }

        /// <summary>
        /// EventLog: the charge point requested a function the server doesn't offer
        /// </summary>
        private void WriteFunctionNotSupported(OCPPMessage msgIn, int? connectorId, string answer, string info)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object> { ["action"] = msgIn.Action, ["answer"] = answer };
            if (!string.IsNullOrEmpty(info)) parameters["info"] = info;
            WriteEvent(connectorId > 0 ? connectorId : null, EventCategory.Station, EventSeverity.Warning, EventCode.FunctionNotSupported,
                parameters, Truncate(msgIn.JsonPayload));
        }

        private static string NameWithInstance(JToken token)
        {
            string name = token?["name"]?.ToString();
            string instance = token?["instance"]?.ToString();
            return string.IsNullOrEmpty(instance) ? name : $"{name}.{instance}";
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxDetailsLength) return text;
            return text.Substring(0, MaxDetailsLength) + "...";
        }
    }
}
