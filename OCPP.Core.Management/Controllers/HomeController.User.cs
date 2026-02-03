/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
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
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult User(string Id, UserViewModel uvm)
        {
            try
            {
                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    Logger.LogWarning("User: Request by non-administrator: {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                List<User> dbUsers = DbContext.Users.OrderBy(x => x.Username).ToList();
                List<ChargeTag> dbChargeTags = DbContext.ChargeTags.OrderBy(x => x.TagName).ToList();
                List<ChargePoint> dbChargePoints = DbContext.ChargePoints.OrderBy(x => x.Name).ToList();
                User currentUser = null;
                if (!string.IsNullOrEmpty(Id) && Id != "@")
                {
                    if (int.TryParse(Id, out int userId))
                    {
                        currentUser = dbUsers.FirstOrDefault(user => user.UserId == userId);
                    }
                }

                if (Request.Method == "POST")
                {
                    string errorMsg = null;

                    if (Id == "@")
                    {
                        if (string.IsNullOrWhiteSpace(uvm.Username))
                        {
                            errorMsg = _localizer["UserNameRequired"].Value;
                        }
                        else if (string.IsNullOrWhiteSpace(uvm.Password))
                        {
                            errorMsg = _localizer["UserPasswordRequired"].Value;
                        }
                        else if (dbUsers.Any(user => user.Username.Equals(uvm.Username, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            errorMsg = _localizer["UserNameExists"].Value;
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            User newUser = new User
                            {
                                Username = uvm.Username,
                                Password = uvm.Password,
                                IsAdmin = uvm.IsAdmin
                            };
                            DbContext.Users.Add(newUser);
                            DbContext.SaveChanges();

                            UpdateUserChargeTags(newUser.UserId, uvm.ChargeTags);
                            UpdateUserChargePoints(newUser.UserId, uvm.ChargePoints);
                            DbContext.SaveChanges();
                        }
                        else
                        {
                            uvm.ChargeTags = BuildChargeTagAssignments(dbChargeTags, uvm.ChargeTags);
                            uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, uvm.ChargePoints);
                            ViewBag.ErrorMsg = errorMsg;
                            return View("UserDetail", uvm);
                        }
                    }
                    else if (currentUser != null)
                    {
                        if (Request.Form["action"] == "Delete")
                        {
                            DbContext.Remove<User>(currentUser);
                            DbContext.SaveChanges();
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(uvm.Username))
                            {
                                errorMsg = _localizer["UserNameRequired"].Value;
                            }
                            else if (dbUsers.Any(user => user.UserId != currentUser.UserId && user.Username.Equals(uvm.Username, StringComparison.InvariantCultureIgnoreCase)))
                            {
                                errorMsg = _localizer["UserNameExists"].Value;
                            }

                            if (string.IsNullOrEmpty(errorMsg))
                            {
                                currentUser.Username = uvm.Username;
                                if (!string.IsNullOrWhiteSpace(uvm.Password))
                                {
                                    currentUser.Password = uvm.Password;
                                }
                                currentUser.IsAdmin = uvm.IsAdmin;
                                DbContext.SaveChanges();

                                UpdateUserChargeTags(currentUser.UserId, uvm.ChargeTags);
                                UpdateUserChargePoints(currentUser.UserId, uvm.ChargePoints);
                                DbContext.SaveChanges();
                            }
                            else
                            {
                                uvm.UserId = currentUser.UserId;
                                uvm.ChargeTags = BuildChargeTagAssignments(dbChargeTags, uvm.ChargeTags);
                                uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, uvm.ChargePoints);
                                ViewBag.ErrorMsg = errorMsg;
                                return View("UserDetail", uvm);
                            }
                        }
                    }

                    return RedirectToAction("User", new { Id = "" });
                }
                else
                {
                    uvm = new UserViewModel
                    {
                        Users = dbUsers
                    };

                    if (currentUser != null)
                    {
                        uvm.UserId = currentUser.UserId;
                        uvm.Username = currentUser.Username;
                        uvm.IsAdmin = currentUser.IsAdmin;
                        HashSet<string> assignedTags = DbContext.UserChargeTags
                            .Where(tag => tag.UserId == currentUser.UserId)
                            .Select(tag => tag.TagId)
                            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                        uvm.ChargeTags = BuildChargeTagAssignments(dbChargeTags, assignedTags);

                        HashSet<string> assignedChargePoints = DbContext.UserChargePoints
                            .Where(point => point.UserId == currentUser.UserId)
                            .Select(point => point.ChargePointId)
                            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                        uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, assignedChargePoints);
                    }
                    else
                    {
                        uvm.ChargeTags = BuildChargeTagAssignments(dbChargeTags, new HashSet<string>(StringComparer.InvariantCultureIgnoreCase));
                        uvm.ChargePoints = BuildChargePointAssignments(dbChargePoints, new HashSet<string>(StringComparer.InvariantCultureIgnoreCase));
                    }

                    string viewName = (!string.IsNullOrEmpty(Id) || Id == "@") ? "UserDetail" : "UserList";
                    return View(viewName, uvm);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "User: Error loading or saving users from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult MyChargeTags()
        {
            try
            {
                int? userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    Logger.LogWarning("MyChargeTags: Missing user id claim for {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                List<ChargeTag> chargeTags = DbContext.UserChargeTags
                    .Include(tag => tag.ChargeTag)
                    .Where(tag => tag.UserId == userId.Value)
                    .Select(tag => tag.ChargeTag)
                    .OrderBy(tag => tag.TagName)
                    .ToList();

                MyChargeTagsViewModel viewModel = new MyChargeTagsViewModel
                {
                    ChargeTags = chargeTags
                };

                return View("MyChargeTags", viewModel);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "MyChargeTags: Error loading charge tags from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        private List<UserChargeTagAssignmentViewModel> BuildChargeTagAssignments(IEnumerable<ChargeTag> chargeTags, IEnumerable<UserChargeTagAssignmentViewModel> selectedAssignments)
        {
            HashSet<string> selectedTagIds = selectedAssignments == null
                ? new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                : selectedAssignments.Where(tag => tag.IsAssigned).Select(tag => tag.TagId).ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            return BuildChargeTagAssignments(chargeTags, selectedTagIds);
        }

        private List<UserChargeTagAssignmentViewModel> BuildChargeTagAssignments(IEnumerable<ChargeTag> chargeTags, HashSet<string> assignedTagIds)
        {
            List<UserChargeTagAssignmentViewModel> assignments = new List<UserChargeTagAssignmentViewModel>();

            foreach (ChargeTag chargeTag in chargeTags)
            {
                assignments.Add(new UserChargeTagAssignmentViewModel
                {
                    TagId = chargeTag.TagId,
                    TagName = chargeTag.TagName,
                    IsAssigned = assignedTagIds.Contains(chargeTag.TagId)
                });
            }

            return assignments;
        }

        private List<UserChargePointAssignmentViewModel> BuildChargePointAssignments(IEnumerable<ChargePoint> chargePoints, IEnumerable<UserChargePointAssignmentViewModel> selectedAssignments)
        {
            HashSet<string> selectedChargePointIds = selectedAssignments == null
                ? new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                : selectedAssignments.Where(point => point.IsAssigned)
                    .Select(point => point.ChargePointId)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            return BuildChargePointAssignments(chargePoints, selectedChargePointIds);
        }

        private List<UserChargePointAssignmentViewModel> BuildChargePointAssignments(IEnumerable<ChargePoint> chargePoints, HashSet<string> assignedChargePointIds)
        {
            List<UserChargePointAssignmentViewModel> assignments = new List<UserChargePointAssignmentViewModel>();

            foreach (ChargePoint chargePoint in chargePoints)
            {
                assignments.Add(new UserChargePointAssignmentViewModel
                {
                    ChargePointId = chargePoint.ChargePointId,
                    ChargePointName = chargePoint.Name,
                    IsAssigned = assignedChargePointIds.Contains(chargePoint.ChargePointId)
                });
            }

            return assignments;
        }

        private void UpdateUserChargeTags(int userId, IEnumerable<UserChargeTagAssignmentViewModel> assignments)
        {
            if (assignments == null)
            {
                return;
            }

            HashSet<string> assignedTagIds = assignments
                .Where(tag => tag.IsAssigned)
                .Select(tag => tag.TagId)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            List<UserChargeTag> existingAssignments = DbContext.UserChargeTags
                .Where(tag => tag.UserId == userId)
                .ToList();

            foreach (UserChargeTag assignment in existingAssignments)
            {
                if (!assignedTagIds.Contains(assignment.TagId))
                {
                    DbContext.UserChargeTags.Remove(assignment);
                }
            }

            foreach (string tagId in assignedTagIds)
            {
                if (!existingAssignments.Any(tag => tag.TagId.Equals(tagId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    DbContext.UserChargeTags.Add(new UserChargeTag
                    {
                        UserId = userId,
                        TagId = tagId
                    });
                }
            }
        }

        private void UpdateUserChargePoints(int userId, IEnumerable<UserChargePointAssignmentViewModel> assignments)
        {
            if (assignments == null)
            {
                return;
            }

            HashSet<string> assignedChargePointIds = assignments
                .Where(point => point.IsAssigned)
                .Select(point => point.ChargePointId)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            List<UserChargePoint> existingAssignments = DbContext.UserChargePoints
                .Where(point => point.UserId == userId)
                .ToList();

            foreach (UserChargePoint assignment in existingAssignments)
            {
                if (!assignedChargePointIds.Contains(assignment.ChargePointId))
                {
                    DbContext.UserChargePoints.Remove(assignment);
                }
            }

            foreach (string chargePointId in assignedChargePointIds)
            {
                if (!existingAssignments.Any(point => point.ChargePointId.Equals(chargePointId, StringComparison.InvariantCultureIgnoreCase)))
                {
                    DbContext.UserChargePoints.Add(new UserChargePoint
                    {
                        UserId = userId,
                        ChargePointId = chargePointId
                    });
                }
            }
        }
    }
}
