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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
        public IActionResult Index()
        {
            Logger.LogTrace("Index: Loading charge points with latest transactions...");

            List<ChargePointsOverviewViewModel> chargePoints = new List<ChargePointsOverviewViewModel>();
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
                        }
                        else
                        {
                            cpovm.MeterStart = -1;
                            cpovm.MeterStop = -1;
                            cpovm.StartTime = null;
                            cpovm.StopTime = null;
                        }
                        chargePoints.Add(cpovm);
                    }

                    Logger.LogInformation("Index: Found {0} charge points", i);

                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Index: Error loading charge points from database");
                return RedirectToAction("Error", new { Id = "" });
            }

            return View(chargePoints);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Logger.LogError("Index-Error: Error called!");
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
