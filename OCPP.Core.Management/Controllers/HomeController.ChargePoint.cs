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
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargePoint(string Id, ChargePointViewModel cpvm)
        {
            try
            {
                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    Logger.LogWarning("ChargePoint: Request by non-administrator: {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                cpvm.CurrentId = Id;

                Logger.LogTrace("ChargePoint: Loading charge points...");
                List<ChargePoint> dbChargePoints = DbContext.ChargePoints.ToList<ChargePoint>();
                Logger.LogInformation("ChargePoint: Found {0} charge points", dbChargePoints.Count);

                ChargePoint currentChargePoint = null;
                if (!string.IsNullOrEmpty(Id))
                {
                    foreach (ChargePoint cp in dbChargePoints)
                    {
                        if (cp.ChargePointId.Equals(Id, StringComparison.InvariantCultureIgnoreCase))
                        {
                            currentChargePoint = cp;
                            Logger.LogTrace("ChargePoint: Current charge point: {0} / {1}", cp.ChargePointId, cp.Name);
                            break;
                        }
                    }
                }

                if (Request.Method == "POST")
                {
                    string errorMsg = null;

                    if (Id == "@")
                    {
                        Logger.LogTrace("ChargePoint: Creating new charge point...");

                        // Create new tag
                        if (string.IsNullOrWhiteSpace(cpvm.ChargePointId))
                        {
                            errorMsg = _localizer["ChargePointIdRequired"].Value;
                            Logger.LogInformation("ChargePoint: New => no charge point ID entered");
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            // check if duplicate
                            foreach (ChargePoint cp in dbChargePoints)
                            {
                                if (cp.ChargePointId.Equals(cpvm.ChargePointId, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // id already exists
                                    errorMsg = _localizer["ChargePointIdExists"].Value;
                                    Logger.LogInformation("ChargePoint: New => charge point ID already exists: {0}", cpvm.ChargePointId);
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            // Save tag in DB
                            ChargePoint newChargePoint = new ChargePoint();
                            newChargePoint.ChargePointId = cpvm.ChargePointId;
                            newChargePoint.Name = cpvm.Name;
                            newChargePoint.Comment = cpvm.Comment;
                            newChargePoint.Username = cpvm.Username;
                            newChargePoint.Password = cpvm.Password;
                            newChargePoint.ClientCertThumb = cpvm.ClientCertThumb;
                            DbContext.ChargePoints.Add(newChargePoint);
                            DbContext.SaveChanges();
                            Logger.LogInformation("ChargePoint: New => charge point saved: {0} / {1}", cpvm.ChargePointId, cpvm.Name);
                        }
                        else
                        {
                            ViewBag.ErrorMsg = errorMsg;
                            return View("ChargePointDetail", cpvm);
                        }
                    }
                    else if (currentChargePoint.ChargePointId == Id)
                    {
                        // Save existing charge point
                        Logger.LogTrace("ChargePoint: Saving charge point '{0}'", Id);
                        currentChargePoint.Name = cpvm.Name;
                        currentChargePoint.Comment = cpvm.Comment;
                        currentChargePoint.Username = cpvm.Username;
                        currentChargePoint.Password = cpvm.Password;
                        currentChargePoint.ClientCertThumb = cpvm.ClientCertThumb;

                        DbContext.SaveChanges();
                        Logger.LogInformation("ChargePoint: Edit => charge point saved: {0} / {1}", cpvm.ChargePointId, cpvm.Name);
                    }

                    return RedirectToAction("ChargePoint", new { Id = "" });
                }
                else
                {
                    // Display charge point
                    cpvm = new ChargePointViewModel();
                    cpvm.ChargePoints = dbChargePoints;
                    cpvm.CurrentId = Id;

                    if (currentChargePoint!= null)
                    {
                        cpvm = new ChargePointViewModel();
                        cpvm.ChargePointId = currentChargePoint.ChargePointId;
                        cpvm.Name = currentChargePoint.Name;
                        cpvm.Comment = currentChargePoint.Comment;
                        cpvm.Username = currentChargePoint.Username;
                        cpvm.Password = currentChargePoint.Password;
                        cpvm.ClientCertThumb = currentChargePoint.ClientCertThumb;
                    }

                    string viewName = (!string.IsNullOrEmpty(cpvm.ChargePointId) || Id == "@") ? "ChargePointDetail" : "ChargePointList";
                    return View(viewName, cpvm);
                }
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
