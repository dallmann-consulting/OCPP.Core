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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Messages_Api;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Result of a request from the server to a chargepoint (see OCPPMiddleware.SendRequestAndWait)
    /// </summary>
    public class OcppRequestResult
    {
        /// <summary>
        /// Result of the answer processing in the controller (null = no valid answer)
        /// </summary>
        public string Answer { get; private set; }

        /// <summary>
        /// Status without valid answer: "Timeout", "Disconnected" or "CallError" (null if there is an answer)
        /// </summary>
        public string NoAnswerStatus { get; private set; }

        /// <summary>
        /// Error code of a CALLERROR from the chargepoint
        /// </summary>
        public string ErrorCode { get; private set; }

        /// <summary>
        /// Error description of a CALLERROR from the chargepoint
        /// </summary>
        public string ErrorDescription { get; private set; }

        /// <summary>
        /// Error code and description as one text for API results (null without CALLERROR)
        /// </summary>
        public string ErrorInfo => string.IsNullOrEmpty(ErrorCode) ? null :
            (string.IsNullOrEmpty(ErrorDescription) ? ErrorCode : $"{ErrorCode}: {ErrorDescription}");

        /// <summary>
        /// API result JSON: the answer - or the status (with error code/description for a CALLERROR)
        /// </summary>
        public string ApiResult
        {
            get
            {
                if (Answer != null) return Answer;

                JObject result = new JObject { ["status"] = NoAnswerStatus };
                if (!string.IsNullOrEmpty(ErrorCode))
                {
                    result["errorCode"] = ErrorCode;
                    result["errorDescription"] = ErrorDescription ?? string.Empty;
                }
                return result.ToString(Formatting.None);
            }
        }

        public static OcppRequestResult FromAnswer(string answer) => new OcppRequestResult() { Answer = answer };

        public static OcppRequestResult FromNoAnswer(string status) => new OcppRequestResult() { NoAnswerStatus = status };

        public static OcppRequestResult FromCallError(string errorCode, string errorDescription) => new OcppRequestResult()
        {
            NoAnswerStatus = ApiVariableStatus.CallError,
            ErrorCode = errorCode,
            ErrorDescription = errorDescription
        };
    }
}
