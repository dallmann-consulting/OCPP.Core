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
