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
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Localization;
using OCPP.Core.Database;

namespace OCPP.Core.Management
{
    /// <summary>
    /// Understandable texts for the events of the EventLog (created from event code and parameters,
    /// the database contains no texts => language of the user, new wording also for old events)
    /// </summary>
    public class EventDisplay
    {
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IReadOnlyDictionary<string, string> _tagNames;
        private Dictionary<string, string> _parameters;

        public EventDisplay(IStringLocalizer<SharedResource> localizer, IReadOnlyDictionary<string, string> tagNames)
        {
            _localizer = localizer;
            _tagNames = tagNames ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Text of the category
        /// </summary>
        public string Category(string category) => Translate("EventCategory", category);

        /// <summary>
        /// Font Awesome icon (classes) of the severity
        /// </summary>
        public static string SeverityIcon(string severity) => severity switch
        {
            EventSeverity.Error => "fas fa-circle-xmark text-danger",
            EventSeverity.Warning => "fas fa-triangle-exclamation text-warning",
            _ => "fas fa-circle-info text-info"
        };

        /// <summary>
        /// Text of the severity (tooltip of the icon)
        /// </summary>
        public string Severity(string severity) => Translate("EventSeverity", severity);

        /// <summary>
        /// Text of the event
        /// </summary>
        public string Text(ChargePointEvent e)
        {
            _parameters = EventLogExtensions.ReadParameters(e.Parameters);
            List<string> additions = new List<string>();
            string text;

            switch (e.EventCode)
            {
                case EventCode.Connected:
                    text = Format("Event_Connected", P("protocol"));
                    break;
                case EventCode.Disconnected:
                    text = Format("Event_Disconnected", Translate("Close", P("closeStatus")));
                    break;
                case EventCode.ConnectionReplaced:
                    text = Format("Event_ConnectionReplaced");
                    break;
                case EventCode.ConnectionRefused:
                    // reason: "AlreadyConnected" or "UnsupportedProtocol" (with the offered protocols)
                    text = P("reason") == "UnsupportedProtocol"
                        ? Format("Event_ConnectionRefusedProtocol", P("protocols") ?? "–")
                        : Format("Event_ConnectionRefused");
                    break;
                case EventCode.AuthenticationFailed:
                    text = Format("Event_AuthenticationFailed", Translate("AuthMethod", P("method")));
                    break;

                case EventCode.Boot:
                    text = Format("Event_Boot", string.Join(" ", new[] { P("vendor"), P("model") }.Where(s => !string.IsNullOrEmpty(s))));
                    Add(additions, "Event_Firmware", P("firmware"));
                    Add(additions, "Event_BootReason", P("reason"));
                    break;
                case EventCode.FirmwareChanged:
                    text = Format("Event_FirmwareChanged", P("old"), P("new"));
                    break;

                case EventCode.SecurityEvent:
                    text = Format("Event_SecurityEvent", Translate("Security", P("type")));
                    if (!string.IsNullOrEmpty(P("techInfo"))) additions.Add(P("techInfo"));
                    break;
                case EventCode.StationEvent:
                    text = Format("Event_StationEvent", string.Join(" / ", new[] { P("component"), P("variable") }.Where(s => !string.IsNullOrEmpty(s))), P("value"));
                    if (P("cleared") == "true") additions.Add(Format("Event_StationEventCleared"));
                    if (!string.IsNullOrEmpty(P("techCode"))) additions.Add(P("techCode"));
                    break;
                case EventCode.FunctionNotSupported:
                    text = Format("Event_FunctionNotSupported", Translate("Function", P("action")), Translate("Result", P("answer")));
                    if (!string.IsNullOrEmpty(P("info"))) additions.Add(P("info"));
                    break;
                case EventCode.UnexpectedMessage:
                    text = Format("Event_UnexpectedMessage", P("action"));
                    break;
                case EventCode.UnsupportedMessage:
                    text = Format("Event_UnsupportedMessage", P("action"));
                    break;

                case EventCode.StatusChanged:
                    text = Format("Event_StatusChanged", P("old") == null ? "–" : Translate("Status", P("old")), Translate("Status", P("new")));
                    Add(additions, "Event_StatusError", P("errorCode"));
                    if (!string.IsNullOrEmpty(P("info"))) additions.Add(P("info"));
                    break;

                case EventCode.TokenAccepted:
                    text = Format("Event_TokenAccepted", Tag("tag"), Translate("AuthAction", P("action")));
                    break;
                case EventCode.TokenRejected:
                    text = Format("Event_TokenRejected", Tag("tag"), Translate("AuthStatus", P("status")), Translate("AuthAction", P("action")));
                    break;

                case EventCode.TransactionStarted:
                    text = Format("Event_TransactionStarted", P("transactionId"));
                    Add(additions, "Event_Token", Tag("tag"));
                    Add(additions, "Event_MeterStart", Number("meterStart"));
                    Add(additions, "Event_RemoteStartId", P("remoteStartId"));
                    break;
                case EventCode.TransactionStopped:
                    text = Format("Event_TransactionStopped", P("transactionId"));
                    Add(additions, "Event_Energy", Number("energy"));
                    Add(additions, "Event_StopReason", P("reason"));
                    Add(additions, "Event_Token", Tag("tag"));
                    break;
                case EventCode.TransactionClosedAutomatically:
                    text = Format("Event_TransactionClosedAutomatically", P("transactionId"));
                    Add(additions, "Event_Energy", Number("energy"));
                    break;
                case EventCode.StopTagNotPermitted:
                    text = Format("Event_StopTagNotPermitted", P("transactionId"), Tag("stopTag"), Tag("startTag"));
                    break;
                case EventCode.MeterDecreased:
                    text = Format("Event_MeterDecreased", P("transactionId"), Number("last") ?? Number("start"), Number("new"),
                        Format(P("takenOver") == "true" ? "Event_MeterTakenOver" : "Event_MeterIgnored"));
                    break;

                case EventCode.Command:
                    text = Format("Event_Command", Translate("Command", P("action")), Translate("Result", P("result")));
                    Add(additions, "Event_CommandUser", P("user"));
                    if (!string.IsNullOrEmpty(P("type"))) additions.Add(P("type"));
                    Add(additions, "Event_Token", Tag("tag"));
                    Add(additions, "Event_RemoteStartId", P("remoteStartId"));
                    if (!string.IsNullOrEmpty(P("key"))) additions.Add($"{P("key")} = {P("value")}");
                    if (!string.IsNullOrEmpty(P("variables"))) additions.Add(P("variables"));
                    if (!string.IsNullOrEmpty(P("error"))) additions.Add(P("error"));
                    break;

                default:
                    // unknown event code (e.g. newer server) => code and parameters
                    text = e.EventCode;
                    additions.AddRange(_parameters.Select(p => $"{p.Key}={p.Value}"));
                    break;
            }

            return additions.Count == 0 ? text : $"{text} ({string.Join(", ", additions)})";
        }

        private string P(string name) => _parameters.TryGetValue(name, out string value) && !string.IsNullOrEmpty(value) ? value : null;

        /// <summary>
        /// Tag with its name (if known)
        /// </summary>
        private string Tag(string name)
        {
            string tag = P(name);
            if (tag == null) return null;
            return _tagNames.TryGetValue(tag, out string tagName) && !string.IsNullOrEmpty(tagName) && tagName != tag ? $"{tagName} ({tag})" : tag;
        }

        /// <summary>
        /// Number (JSON invariant) in the culture of the user
        /// </summary>
        private string Number(string name)
        {
            string value = P(name);
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d.ToString("0.###", CultureInfo.CurrentCulture) : value;
        }

        private string Format(string key, params object[] args)
        {
            LocalizedString format = _localizer[key];
            return args.Length == 0 ? format.Value : string.Format(format.Value, args);
        }

        private void Add(List<string> additions, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) additions.Add(Format(key, value));
        }

        /// <summary>
        /// Text for a value (e.g. "Status_Faulted") - or the value itself if there is no text
        /// </summary>
        private string Translate(string prefix, string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            LocalizedString text = _localizer[$"{prefix}_{value}"];
            return text.ResourceNotFound ? value : text.Value;
        }
    }
}
