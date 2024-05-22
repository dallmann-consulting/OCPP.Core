using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OCPP.Core.Management.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargeReport(DateTime? startTime, DateTime? stopTime)
        {
            try
            {
                startTime ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1); // First day of the previous month
                stopTime = stopTime.HasValue ? stopTime.Value.Date.AddDays(1).AddSeconds(-1) : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddSeconds(-1); // Default to last second of previous month

                var transactions = DbContext.Set<Transaction>()
                    .Where(t => t.StartTime >= startTime && (!t.StopTime.HasValue || t.StopTime <= stopTime))
                    .ToList();

                var tags = DbContext.Set<ChargeTag>().ToList();

                var report = new ChargeReportViewModel
                {
                    StartTime = startTime.Value,
                    StopTime = stopTime.Value,
                    Groups = transactions
                        .GroupBy(t => t.StartTag.ParentTagId)
                        .OrderBy(g => g.Key) // Order groups by name
                        .Select(g => new GroupReport
                        {
                            GroupName = g.Key,
                            Tags = g.GroupBy(t => tags.FirstOrDefault(tag => tag.TagId == t.StartTag.TagId)?.TagName)
                                    .OrderBy(tg => tg.Key) // Order tags by name
                                    .Select(tg => new TagReport
                                    {
                                        TagName = tg.Key,
                                        Transactions = tg.Select(t => new TransactionReport
                                        {
                                            TransactionId = t.TransactionId,
                                            ChargePointId = t.ChargePointId,
                                            ConnectorId = t.ConnectorId,
                                            StartTagId = t.StartTag.TagId,
                                            StartTime = t.StartTime,
                                            MeterStart = t.MeterStart,
                                            StartResult = t.StartResult,
                                            StopTagId = t.StartTag.TagId,
                                            StopTime = t.StopTime,
                                            MeterStop = t.MeterStop,
                                            StopReason = t.StopReason
                                        }).ToList()
                                    }).ToList()
                        }).ToList()
                };

                return View(report);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error loading charge points from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }
    }
}
