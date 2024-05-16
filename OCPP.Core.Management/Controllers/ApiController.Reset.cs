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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    public partial class ApiController : BaseController
    {
        private readonly IStringLocalizer<ApiController> _localizer;

        public ApiController(
            UserManager userManager,
            IStringLocalizer<ApiController> localizer,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext) : base(userManager, loggerFactory, config, dbContext)
        {
            _localizer = localizer;
            Logger = loggerFactory.CreateLogger<ApiController>();
        }

        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Reset(string Id)
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName))
            {
                Logger.LogWarning("Reset: Request by non-administrator: {0}", User?.Identity?.Name);
                return StatusCode((int)HttpStatusCode.Unauthorized);
            }

            int httpStatuscode = (int)HttpStatusCode.OK;
            string resultContent = string.Empty;

            Logger.LogTrace("Reset: Request to restart chargepoint '{0}'", Id);
            if (!string.IsNullOrEmpty(Id))
            {
                try
                {
                    ChargePoint chargePoint = DbContext.ChargePoints.Find(Id);
                    if (chargePoint != null)
                    {
                        string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                        string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                        if (!string.IsNullOrEmpty(serverApiUrl))
                        {
                            try
                            {
                                using (var httpClient = new HttpClient())
                                {
                                    if (!serverApiUrl.EndsWith('/'))
                                    {
                                        serverApiUrl += "/";
                                    }
                                    Uri uri = new Uri(serverApiUrl);
                                    uri = new Uri(uri, $"Reset/{Uri.EscapeDataString(Id)}");
                                    httpClient.Timeout = new TimeSpan(0, 0, 4); // use short timeout

                                    // API-Key authentication?
                                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                                    {
                                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                                    }
                                    else
                                    {
                                        Logger.LogWarning("Reset: No API-Key configured!");
                                    }

                                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                                    if (response.StatusCode == HttpStatusCode.OK)
                                    {
                                        string jsonResult = await response.Content.ReadAsStringAsync();
                                        if (!string.IsNullOrEmpty(jsonResult))
                                        {
                                            try
                                            {
                                                dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                                                Logger.LogInformation("Reset: Result of API request is '{0}'", jsonResult);
                                                string status = jsonObject.status;
                                                switch (status)
                                                {
                                                    case "Accepted":
                                                        resultContent = _localizer["ResetAccepted"];
                                                        break;
                                                    case "Rejected":
                                                        resultContent = _localizer["ResetRejected"];
                                                        break;
                                                    case "Scheduled":
                                                        resultContent = _localizer["ResetScheduled"];
                                                        break;
                                                    default:
                                                        resultContent = string.Format(_localizer["ResetUnknownStatus"], status);
                                                        break;
                                                }
                                            }
                                            catch (Exception exp)
                                            {
                                                Logger.LogError(exp, "Reset: Error in JSON result => {0}", exp.Message);
                                                httpStatuscode = (int)HttpStatusCode.OK;
                                                resultContent = _localizer["ResetError"];
                                            }
                                        }
                                        else
                                        {
                                            Logger.LogError("Reset: Result of API request is empty");
                                            httpStatuscode = (int)HttpStatusCode.OK;
                                            resultContent = _localizer["ResetError"];
                                        }
                                    }
                                    else if (response.StatusCode == HttpStatusCode.NotFound)
                                    {
                                        // Chargepoint offline
                                        httpStatuscode = (int)HttpStatusCode.OK;
                                        resultContent = _localizer["ResetOffline"];
                                    }
                                    else
                                    {
                                        Logger.LogError("Reset: Result of API  request => httpStatus={0}", response.StatusCode);
                                        httpStatuscode = (int)HttpStatusCode.OK;
                                        resultContent = _localizer["ResetError"];
                                    }
                                }
                            }
                            catch (Exception exp)
                            {
                                Logger.LogError(exp, "Reset: Error in API request => {0}", exp.Message);
                                httpStatuscode = (int)HttpStatusCode.OK;
                                resultContent = _localizer["ResetError"];
                            }
                        }
                    }
                    else
                    {
                        Logger.LogWarning("Reset: Error loading charge point '{0}' from database", Id);
                        httpStatuscode = (int)HttpStatusCode.OK;
                        resultContent = _localizer["UnknownChargepoint"];
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "Reset: Error loading charge point from database");
                    httpStatuscode = (int)HttpStatusCode.OK;
                    resultContent = _localizer["ResetError"];
                }
            }

            return StatusCode(httpStatuscode, resultContent);
        }
    }
}
