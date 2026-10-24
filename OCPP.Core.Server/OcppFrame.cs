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
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Parses and creates OCPP-J frames (JSON arrays):
    ///   CALL            [2, "id", "Action", {payload}]
    ///   CALLRESULT      [3, "id", {payload}]
    ///   CALLERROR       [4, "id", "errorCode", "errorDescription", {errorDetails}]
    ///   CALLRESULTERROR [5, "id", "errorCode", "errorDescription", {errorDetails}]  (OCPP 2.1)
    ///   SEND            [6, "id", "Action", {payload}]                              (OCPP 2.1)
    /// </summary>
    public static class OcppFrame
    {
        /// <summary>
        /// Message ID for a CALLERROR when the message ID of a corrupt message can't be read (OCPP-J 2.0.1/2.1)
        /// </summary>
        public const string UnknownMessageId = "-1";

        /// <summary>
        /// Parses an OCPP-J frame.
        /// Returns false for an invalid frame: "message" then contains the parts that could be read
        /// (message type and/or ID, e.g. for a CALLERROR response) and "error" a description.
        /// </summary>
        public static bool TryParse(string text, out OCPPMessage message, out string error)
        {
            message = new OCPPMessage();
            error = null;

            JArray frame;
            try
            {
                // no conversion of dates and numbers => payload stays as sent by the charge point
                using JsonTextReader reader = new JsonTextReader(new StringReader(text ?? string.Empty))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal
                };
                frame = JToken.ReadFrom(reader) as JArray;
                if (reader.Read())
                {
                    // additional content after the array
                    frame = null;
                }
            }
            catch (JsonException exp)
            {
                error = $"Invalid JSON: {exp.Message}";
                return false;
            }

            if (frame == null || frame.Count < 3)
            {
                error = "Message is not a JSON array with at least 3 elements";
                return false;
            }

            if (frame[1].Type == JTokenType.String || frame[1].Type == JTokenType.Integer)
            {
                message.UniqueId = frame[1].ToString();
            }
            if (frame[0].Type != JTokenType.Integer)
            {
                error = "Message type is not a number";
                return false;
            }
            message.MessageType = frame[0].ToString();
            if (string.IsNullOrEmpty(message.UniqueId))
            {
                error = "Message ID is missing";
                return false;
            }

            switch (message.MessageType)
            {
                case OcppMessageType.Call:
                case OcppMessageType.Send:
                    if (frame.Count != 4 || frame[2].Type != JTokenType.String || frame[3].Type != JTokenType.Object)
                    {
                        error = "Expected [type, \"id\", \"Action\", {payload}]";
                        return false;
                    }
                    message.Action = frame[2].ToString();
                    message.JsonPayload = frame[3].ToString(Formatting.None);
                    return true;

                case OcppMessageType.CallResult:
                    if (frame.Count != 3 || frame[2].Type != JTokenType.Object)
                    {
                        error = "Expected [3, \"id\", {payload}]";
                        return false;
                    }
                    message.JsonPayload = frame[2].ToString(Formatting.None);
                    return true;

                case OcppMessageType.CallError:
                case OcppMessageType.CallResultError:
                    if (frame.Count < 4 || frame.Count > 5 || frame[2].Type != JTokenType.String)
                    {
                        error = "Expected [type, \"id\", \"errorCode\", \"errorDescription\", {errorDetails}]";
                        return false;
                    }
                    message.ErrorCode = frame[2].ToString();
                    message.ErrorDescription = frame[3].ToString();
                    message.JsonPayload = (frame.Count == 5) ? frame[4].ToString(Formatting.None) : "{}";
                    return true;

                default:
                    error = $"Unknown message type '{message.MessageType}'";
                    return false;
            }
        }

        /// <summary>
        /// Creates the OCPP-J frame for a message (with correct JSON escaping of all strings).
        /// A message with error code is sent as CALLERROR.
        /// </summary>
        public static string Serialize(OCPPMessage message)
        {
            JArray frame;
            if (!string.IsNullOrEmpty(message.ErrorCode))
            {
                frame = new JArray(int.Parse(OcppMessageType.CallError), message.UniqueId, message.ErrorCode, message.ErrorDescription ?? string.Empty, new JObject());
            }
            else if (message.MessageType == OcppMessageType.Call)
            {
                frame = new JArray(int.Parse(OcppMessageType.Call), message.UniqueId, message.Action, new JRaw(PayloadOrEmpty(message)));
            }
            else
            {
                frame = new JArray(int.Parse(OcppMessageType.CallResult), message.UniqueId, new JRaw(PayloadOrEmpty(message)));
            }
            return frame.ToString(Formatting.None);
        }

        private static string PayloadOrEmpty(OCPPMessage message)
        {
            return string.IsNullOrWhiteSpace(message.JsonPayload) ? "{}" : message.JsonPayload;
        }
    }
}
