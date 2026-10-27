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
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        /// <summary>
        /// Events (EventLog) of a charge point, newest first (only for administrators).
        /// Filter: t = time span (1 = 30, 2 = 90, 3 = 365 days), category, severity (minimum), page
        /// </summary>
        [Authorize]
        public IActionResult ChargePointEvents(string Id, int? t, string category, string severity, int? page)
        {
            try
            {
                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    Logger.LogWarning("ChargePointEvents: Request by non-administrator: {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                ChargePoint chargePoint = string.IsNullOrEmpty(Id) ? null : DbContext.ChargePoints.Find(Id);
                if (chargePoint == null)
                {
                    Logger.LogWarning("ChargePointEvents: Unknown chargepoint '{0}'", Id);
                    return RedirectToAction("ChargePoint", new { Id = "" });
                }

                ChargePointEventsViewModel model = new ChargePointEventsViewModel
                {
                    ChargePointId = chargePoint.ChargePointId,
                    ChargePointName = chargePoint.Name,
                    Timespan = (t == 2 || t == 3) ? t.Value : 1,
                    Category = EventCategory.All.Contains(category) ? category : null,
                    Severity = (severity == EventSeverity.Warning || severity == EventSeverity.Error) ? severity : null
                };

                int days = model.Timespan switch { 2 => 90, 3 => 365, _ => 30 };
                DateTime from = DateTime.UtcNow.AddDays(-days);

                IQueryable<ChargePointEvent> query = DbContext.ChargePointEvents.AsNoTracking()
                    .Where(e => e.ChargePointId == chargePoint.ChargePointId && e.EventTime >= from);
                if (model.Category != null)
                {
                    query = query.Where(e => e.Category == model.Category);
                }
                if (model.Severity == EventSeverity.Warning)
                {
                    query = query.Where(e => e.Severity == EventSeverity.Warning || e.Severity == EventSeverity.Error);
                }
                else if (model.Severity == EventSeverity.Error)
                {
                    query = query.Where(e => e.Severity == EventSeverity.Error);
                }

                model.TotalCount = query.Count();
                model.PageCount = Math.Max(1, (model.TotalCount + ChargePointEventsViewModel.PageSize - 1) / ChargePointEventsViewModel.PageSize);
                model.Page = Math.Clamp(page ?? 1, 1, model.PageCount);
                model.Events = query
                    .OrderByDescending(e => e.EventTime)
                    .ThenByDescending(e => e.EventId)
                    .Skip((model.Page - 1) * ChargePointEventsViewModel.PageSize)
                    .Take(ChargePointEventsViewModel.PageSize)
                    .ToList();

                // names of the connectors and the tags of this page
                foreach (ConnectorStatus cs in DbContext.ConnectorStatuses.AsNoTracking().Where(cs => cs.ChargePointId == chargePoint.ChargePointId))
                {
                    model.ConnectorNames[cs.ConnectorId] = string.IsNullOrEmpty(cs.ConnectorName) ? cs.ConnectorId.ToString() : $"{cs.ConnectorName} ({cs.ConnectorId})";
                }
                HashSet<string> tags = new HashSet<string>();
                foreach (ChargePointEvent e in model.Events)
                {
                    Dictionary<string, string> parameters = EventLogExtensions.ReadParameters(e.Parameters);
                    foreach (string name in new[] { "tag", "startTag", "stopTag" })
                    {
                        if (parameters.TryGetValue(name, out string tag) && !string.IsNullOrEmpty(tag)) tags.Add(tag);
                    }
                }
                if (tags.Count > 0)
                {
                    model.TagNames = DbContext.ChargeTags.AsNoTracking()
                        .Where(ct => tags.Contains(ct.TagId))
                        .ToDictionary(ct => ct.TagId, ct => ct.TagName);
                }

                return View("ChargePointEvents", model);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePointEvents: Error loading events");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }
    }
}
