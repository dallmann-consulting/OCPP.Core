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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        private readonly IStringLocalizer<HomeController> _localizer;

        public HomeController(
            UserManager userManager,
            IStringLocalizer<HomeController> localizer,
            ILoggerFactory loggerFactory,
            IConfiguration config) : base(userManager, loggerFactory, config)
        {
            _localizer = localizer;
            Logger = loggerFactory.CreateLogger<HomeController>();
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            Logger.LogTrace("Index: Loading charge points with latest transactions...");

            OverviewViewModel overviewModel = new OverviewViewModel();
            overviewModel.ChargePoints = new List<ChargePointsOverviewViewModel>();
            try
            {
                using (OCPPCoreContext dbContext = new OCPPCoreContext(this.Config))
                {
                    var chargeData = from cp in dbContext.ChargePoints
                                      from t in cp.Transactions
                                      .OrderByDescending(t => t.TransactionId)
                                      .DefaultIfEmpty()
                                      .Take(1)
                                      select new { cp, t };

                    ChargePointStatus[] statusList = null;

                    string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                    string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                    if (!string.IsNullOrEmpty(serverApiUrl))
                    {
                        bool serverError = false;
                        try
                        {
                            using (var httpClient = new HttpClient())
                            {
                                if (!serverApiUrl.EndsWith('/'))
                                {
                                    serverApiUrl += "/";
                                }
                                Uri uri = new Uri(serverApiUrl);
                                uri = new Uri(uri, "Status");
                                httpClient.Timeout = new TimeSpan(0, 0, 4); // use short timeout

                                // API-Key authentication?
                                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                                {
                                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                                }
                                else
                                {
                                    Logger.LogWarning("Index: No API-Key configured!");
                                }

                                HttpResponseMessage response = await httpClient.GetAsync(uri);
                                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    string jsonData = await response.Content.ReadAsStringAsync();
                                    if (!string.IsNullOrEmpty(jsonData))
                                    {
                                        statusList = JsonConvert.DeserializeObject<ChargePointStatus[]>(jsonData);
                                        overviewModel.ServerConnection = true;
                                    }
                                    else
                                    {
                                        Logger.LogError("Index: Result of status web request is empty");
                                        serverError = true;
                                    }
                                }
                                else
                                {
                                    Logger.LogError("Index: Result of status web request => httpStatus={0}", response.StatusCode);
                                    serverError = true;
                                }
                            }

                            Logger.LogInformation("Index: Result of status web request => Length={0}", statusList?.Length);
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "Index: Error in status web request => {0}", exp.Message);
                            serverError = true;
                        }

                        if (serverError)
                        {
                            ViewBag.ErrorMsg = _localizer["ErrorOCPPServer"];
                        }
                    }

                    int i = 0;
                    foreach (var cp in chargeData)
                    {
                        // count number of charge points
                        i++;

                        // Copy data in view model
                        ChargePointsOverviewViewModel cpovm = new ChargePointsOverviewViewModel();
                        cpovm.ChargePointId = cp.cp.ChargePointId;
                        cpovm.Name = cp.cp.Name;
                        cpovm.Comment = cp.cp.Comment;
                        if (cp.t != null)
                        {
                            cpovm.MeterStart = cp.t.MeterStart;
                            cpovm.MeterStop = cp.t.MeterStop;
                            cpovm.StartTime = cp.t.StartTime;
                            cpovm.StopTime = cp.t.StopTime;

                            // default status: active transaction or not?
                            cpovm.ConnectorStatus = (cpovm.StopTime.HasValue) ? ConnectorStatus.Available : ConnectorStatus.Occupied;
                        }
                        else
                        {
                            cpovm.MeterStart = -1;
                            cpovm.MeterStop = -1;
                            cpovm.StartTime = null;
                            cpovm.StopTime = null;

                            // default status: Available
                            cpovm.ConnectorStatus = ConnectorStatus.Available;
                        }
                        overviewModel.ChargePoints.Add(cpovm);

                        if (statusList != null)
                        {
                            foreach (ChargePointStatus cpStatus in statusList)
                            {
                                if (cpStatus.Id.Equals(cpovm.ChargePointId, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // ChargePoint in statuslist => online
                                    cpovm.Online = true;

                                    if (cpStatus.EVSE1Status != ConnectorStatus.Undefined)
                                    {
                                        // TODO: display multiple connectors
                                        Logger.LogTrace("Index: Charge point '{0}' => Override status '{1}' to '{2}'", cpovm.ChargePointId, cpovm.ConnectorStatus, cpStatus.EVSE1Status);
                                        cpovm.ConnectorStatus = cpStatus.EVSE1Status;
                                    }

                                    if (cpovm.ConnectorStatus == ConnectorStatus.Occupied)
                                    {
                                        ChargingData chargingData = cpStatus.ChargingDataEVSE1;
                                        if (chargingData == null)
                                        {
                                            chargingData = cpStatus.ChargingDataEVSE2;
                                        }
                                        if (chargingData != null)
                                        {
                                            string currentCharge = string.Empty;
                                            if (chargingData.ChargeRateKW != null)
                                            {
                                                currentCharge = string.Format("{0:0.0}kW", chargingData.ChargeRateKW.Value);
                                            }
                                            if (chargingData.SoC != null)
                                            {
                                                if (!string.IsNullOrWhiteSpace(currentCharge)) currentCharge += " | ";
                                                currentCharge += string.Format("{0:0}%", chargingData.SoC.Value);
                                            }
                                            if (!string.IsNullOrWhiteSpace(currentCharge))
                                            {
                                                cpovm.CurrentChargeData = currentCharge;
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    Logger.LogInformation("Index: Found {0} charge points", i);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Index: Error loading charge points from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }

            return View(overviewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
