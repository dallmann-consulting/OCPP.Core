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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    public partial class ApiController : BaseController
    {
        /// <summary>
        /// Reads all configuration values of a chargepoint (OCPP 1.6: GetConfiguration / OCPP 2.x: GetBaseReport).
        /// Returns the JSON answer of the OCPP server or {"error": "..."}
        /// </summary>
        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> GetVariables(string Id, string reportBase)
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName))
            {
                Logger.LogWarning("GetVariables: Request by non-administrator: {0}", User?.Identity?.Name);
                return StatusCode((int)HttpStatusCode.Unauthorized);
            }

            Logger.LogTrace("GetVariables: Request to read configuration of chargepoint '{0}' (reportBase={1})", Id, reportBase);
            if (string.IsNullOrEmpty(Id))
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            string apiPath = $"GetVariables/{Uri.EscapeDataString(Id)}";
            if (!string.IsNullOrWhiteSpace(reportBase))
            {
                apiPath += $"?reportBase={Uri.EscapeDataString(reportBase)}";
            }

            // The server collects OCPP 2.x reports for up to 'ReportMaxDuration' seconds => wait a bit longer
            TimeSpan timeout = TimeSpan.FromSeconds(Config.GetValue<int>("ReportMaxDuration", 300) + 30);
            return await SendVariablesRequest("GetVariables", Id, apiPath, null, timeout);
        }

        /// <summary>
        /// Changes one configuration value of a chargepoint (OCPP 1.6: ChangeConfiguration / OCPP 2.x: SetVariables).
        /// Returns the JSON answer of the OCPP server or {"error": "..."}
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> SetVariables(string Id, string component, string componentInstance, int? evseId, int? evseConnectorId,
                                                      string variable, string variableInstance, string attributeType, string value)
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName))
            {
                Logger.LogWarning("SetVariables: Request by non-administrator: {0}", User?.Identity?.Name);
                return StatusCode((int)HttpStatusCode.Unauthorized);
            }

            Logger.LogTrace("SetVariables: Request to change '{0}.{1}' of chargepoint '{2}'", component, variable, Id);
            if (string.IsNullOrEmpty(Id) || string.IsNullOrWhiteSpace(variable))
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            // Build request in the format of the server API (see Server-API.md)
            JObject entry = new JObject();
            if (!string.IsNullOrWhiteSpace(component))
            {
                JObject jComponent = new JObject { ["name"] = component };
                if (!string.IsNullOrEmpty(componentInstance)) jComponent["instance"] = componentInstance;
                if (evseId.HasValue)
                {
                    JObject jEvse = new JObject { ["id"] = evseId.Value };
                    if (evseConnectorId.HasValue) jEvse["connectorId"] = evseConnectorId.Value;
                    jComponent["evse"] = jEvse;
                }
                entry["component"] = jComponent;
            }
            JObject jVariable = new JObject { ["name"] = variable };
            if (!string.IsNullOrEmpty(variableInstance)) jVariable["instance"] = variableInstance;
            entry["variable"] = jVariable;
            if (!string.IsNullOrEmpty(attributeType)) entry["attributeType"] = attributeType;
            // MVC binds an empty input as null => an empty value is a valid configuration value
            entry["value"] = value ?? string.Empty;

            JObject apiRequest = new JObject { ["variables"] = new JArray(entry) };

            // The server waits up to 60s for the chargepoint
            return await SendVariablesRequest("SetVariables", Id, $"SetVariables/{Uri.EscapeDataString(Id)}", apiRequest.ToString(Formatting.None), TimeSpan.FromSeconds(75));
        }

        /// <summary>
        /// Sends a GetVariables/SetVariables request to the OCPP server and passes the JSON answer through
        /// </summary>
        private async Task<IActionResult> SendVariablesRequest(string cmd, string chargePointId, string apiPath, string jsonBody, TimeSpan timeout)
        {
            try
            {
                ChargePoint chargePoint = DbContext.ChargePoints.Find(chargePointId);
                if (chargePoint == null)
                {
                    Logger.LogWarning("{0}: Error loading charge point '{1}' from database", cmd, chargePointId);
                    return Json(new { error = _localizer["UnknownChargepoint"].Value });
                }

                string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                if (string.IsNullOrEmpty(serverApiUrl))
                {
                    Logger.LogError("{0}: No server API URL configured", cmd);
                    return Json(new { error = _localizer["VariablesError"].Value });
                }

                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += "/";
                    }
                    Uri uri = new Uri(new Uri(serverApiUrl), apiPath);
                    httpClient.Timeout = timeout;

                    // API-Key authentication?
                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    {
                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                    }
                    else
                    {
                        Logger.LogWarning("{0}: No API-Key configured!", cmd);
                    }

                    HttpResponseMessage response;
                    if (jsonBody != null)
                    {
                        response = await httpClient.PostAsync(uri, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
                    }
                    else
                    {
                        response = await httpClient.GetAsync(uri);
                    }

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        Logger.LogInformation("{0}: Result of API request has {1} characters", cmd, jsonResult?.Length);
                        if (!string.IsNullOrEmpty(jsonResult))
                        {
                            return Content(jsonResult, "application/json");
                        }
                        Logger.LogError("{0}: Result of API request is empty", cmd);
                        return Json(new { error = _localizer["VariablesError"].Value });
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        // Chargepoint offline
                        return Json(new { error = _localizer["ChargerOffline"].Value });
                    }
                    else
                    {
                        Logger.LogError("{0}: Result of API request => httpStatus={1}", cmd, response.StatusCode);
                        return Json(new { error = _localizer["VariablesError"].Value });
                    }
                }
            }
            catch (TaskCanceledException exp)
            {
                Logger.LogError(exp, "{0}: Timeout in API request => {1}", cmd, exp.Message);
                return Json(new { error = _localizer["Timeout"].Value });
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "{0}: Error in API request => {1}", cmd, exp.Message);
                return Json(new { error = _localizer["VariablesError"].Value });
            }
        }
    }
}
