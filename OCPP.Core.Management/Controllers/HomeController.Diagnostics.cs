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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public async Task<IActionResult> Diagnostics()
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName))
            {
                Logger.LogWarning("Diagnostics: Request by non-administrator: {0}", User?.Identity?.Name);
                TempData["ErrMsgKey"] = "AccessDenied";
                return RedirectToAction("Error", new { Id = "" });
            }

            Logger.LogTrace("Diagnostics: Building connector diagnostics view...");

            var model = new DiagnosticsViewModel();

            // Fetch live status from OCPP server
            Dictionary<string, ChargePointStatus> dictOnlineStatus = new Dictionary<string, ChargePointStatus>();
            string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
            string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
            if (!string.IsNullOrEmpty(serverApiUrl))
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        if (!serverApiUrl.EndsWith('/'))
                            serverApiUrl += "/";
                        Uri uri = new Uri(new Uri(serverApiUrl), "Status");
                        httpClient.Timeout = new TimeSpan(0, 0, 4);

                        if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                        else
                            Logger.LogWarning("Diagnostics: No API-Key configured!");

                        HttpResponseMessage response = await httpClient.GetAsync(uri);
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            string jsonData = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrEmpty(jsonData))
                            {
                                ChargePointStatus[] onlineList = JsonConvert.DeserializeObject<ChargePointStatus[]>(jsonData);
                                if (onlineList != null)
                                {
                                    foreach (ChargePointStatus cps in onlineList)
                                        dictOnlineStatus.TryAdd(cps.Id, cps);
                                    model.ServerOnline = true;
                                }
                            }
                            else
                            {
                                Logger.LogError("Diagnostics: Server status response is empty");
                            }
                        }
                        else
                        {
                            Logger.LogError("Diagnostics: Server status request returned httpStatus={0}", response.StatusCode);
                        }
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "Diagnostics: Error fetching server status => {0}", exp.Message);
                }
            }

            try
            {
                // Load all connector statuses from DB
                List<ConnectorStatus> dbConnectors = DbContext.ConnectorStatuses.AsNoTracking().ToList();

                // Load all open transactions (no stop time)
                List<Transaction> openTransactions = DbContext.Transactions
                    .Where(t => t.StopTime == null)
                    .AsNoTracking()
                    .ToList();

                Logger.LogInformation("Diagnostics: {0} connectors, {1} open transactions, {2} online charge points",
                    dbConnectors.Count, openTransactions.Count, dictOnlineStatus.Count);

                foreach (ConnectorStatus cs in dbConnectors)
                {
                    dictOnlineStatus.TryGetValue(cs.ChargePointId, out ChargePointStatus cpStatus);
                    OnlineConnectorStatus ocs = null;
                    cpStatus?.OnlineConnectors?.TryGetValue(cs.ConnectorId, out ocs);

                    Transaction openTx = openTransactions
                        .Where(t => string.Equals(t.ChargePointId, cs.ChargePointId, StringComparison.OrdinalIgnoreCase)
                                 && t.ConnectorId == cs.ConnectorId)
                        .OrderByDescending(t => t.StartTime)
                        .FirstOrDefault();

                    model.Connectors.Add(new ConnectorDiagnosticsRow
                    {
                        ChargePointId = cs.ChargePointId,
                        ConnectorId = cs.ConnectorId,
                        ConnectorName = cs.ConnectorName,
                        ChargePointOnline = cpStatus != null,
                        OcppProtocol = cpStatus?.Protocol,
                        LiveStatus = ocs?.Status,
                        LiveChargeRateKW = ocs?.ChargeRateKW,
                        LiveMeterKWH = ocs?.MeterKWH,
                        LiveSoC = ocs?.SoC,
                        DbStatus = cs.LastStatus,
                        DbStatusTime = cs.LastStatusTime,
                        DbLastMeter = cs.LastMeter,
                        DbLastMeterTime = cs.LastMeterTime,
                        HasOpenTransaction = openTx != null,
                        OpenTransactionId = openTx?.TransactionId,
                        OpenTransactionStart = openTx?.StartTime,
                        OpenTransactionTagId = openTx?.StartTagId,
                    });
                }

                // Sort: anomalies first, then by charge point ID
                model.Connectors = model.Connectors
                    .OrderByDescending(c => c.IsAnomalous)
                    .ThenBy(c => c.ChargePointId)
                    .ThenBy(c => c.ConnectorId)
                    .ToList();
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Diagnostics: Error loading connector data from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }

            return View(model);
        }
    }
}
