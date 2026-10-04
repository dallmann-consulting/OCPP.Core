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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        /// <summary>
        /// Page for reading and changing the configuration values of a chargepoint.
        /// The values are loaded by the page itself via API/GetVariables and API/SetVariables.
        /// </summary>
        [Authorize]
        public IActionResult ChargePointConfiguration(string Id)
        {
            try
            {
                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    Logger.LogWarning("ChargePointConfiguration: Request by non-administrator: {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                ChargePoint chargePoint = string.IsNullOrEmpty(Id) ? null : DbContext.ChargePoints.Find(Id);
                if (chargePoint == null)
                {
                    Logger.LogWarning("ChargePointConfiguration: Unknown chargepoint '{0}'", Id);
                    return RedirectToAction("ChargePoint", new { Id = "" });
                }

                ChargePointViewModel cpvm = new ChargePointViewModel();
                cpvm.ChargePointId = chargePoint.ChargePointId;
                cpvm.Name = chargePoint.Name;
                return View("ChargePointConfiguration", cpvm);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePointConfiguration: Error loading chargepoint");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }
    }
}
