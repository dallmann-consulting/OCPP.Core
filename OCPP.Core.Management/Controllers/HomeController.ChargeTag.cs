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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargeTag(string Id, ChargeTagViewModel ctvm)
        {
            try
            {
                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    Logger.LogWarning("ChargeTag: Request by non-administrator: {0}", User?.Identity?.Name);
                    TempData["ErrMsgKey"] = "AccessDenied";
                    return RedirectToAction("Error", new { Id = "" });
                }

                ViewBag.DatePattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
                ViewBag.Language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                ctvm.CurrentTagId = Id;

                Logger.LogTrace("ChargeTag: Loading charge tags...");
                List<ChargeTag> dbChargeTags = DbContext.ChargeTags.OrderBy(x => x.TagName).ToList<ChargeTag>();
                List<User> dbUsers = DbContext.Users.OrderBy(x => x.Username).ToList<User>();
                Logger.LogInformation("ChargeTag: Found {0} charge tags", dbChargeTags.Count);

                ChargeTag currentChargeTag = null;
                if (!string.IsNullOrEmpty(Id))
                {
                    foreach (ChargeTag tag in dbChargeTags)
                    {
                        if (tag.TagId.Equals(Id, StringComparison.InvariantCultureIgnoreCase))
                        {
                            currentChargeTag = tag;
                            Logger.LogTrace("ChargeTag: Current charge tag: {0} / {1}", tag.TagId, tag.TagName);
                            break;
                        }
                    }
                }

                if (Request.Method == "POST")
                {
                    string errorMsg = null;

                    if (Id == "@")
                    {
                        Logger.LogTrace("ChargeTag: Creating new charge tag...");

                        // Create new tag
                        if (string.IsNullOrWhiteSpace(ctvm.TagId))
                        {
                            errorMsg = _localizer["ChargeTagIdRequired"].Value;
                            Logger.LogInformation("ChargeTag: New => no charge tag ID entered");
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            // check if duplicate
                            foreach (ChargeTag tag in dbChargeTags)
                            {
                                if (tag.TagId.Equals(ctvm.TagId, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // tag-id already exists
                                    errorMsg = _localizer["ChargeTagIdExists"].Value;
                                    Logger.LogInformation("ChargeTag: New => charge tag ID already exists: {0}", ctvm.TagId);
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            // Save tag in DB
                            ChargeTag newTag = new ChargeTag();
                            newTag.TagId = ctvm.TagId;
                            newTag.TagName = ctvm.TagName;
                            newTag.ParentTagId = ctvm.ParentTagId;
                            newTag.ExpiryDate = ctvm.ExpiryDate;
                            newTag.Blocked = ctvm.Blocked;
                            DbContext.ChargeTags.Add(newTag);
                            DbContext.SaveChanges();
                            UpdateChargeTagUsers(ctvm.TagId, ctvm.Users);
                            DbContext.SaveChanges();
                            Logger.LogInformation("ChargeTag: New => charge tag saved: {0} / {1}", ctvm.TagId, ctvm.TagName);
                        }
                        else
                        {
                            ctvm.Users = BuildUserAssignments(dbUsers, ctvm.Users);
                            ViewBag.ErrorMsg = errorMsg;
                            return View("ChargeTagDetail", ctvm);
                        }
                    }
                    else if (currentChargeTag.TagId == Id)
                    {
                        if (Request.Form["action"] == "Delete")
                        {
                            // Delete existing tag
                            Logger.LogDebug("ChargeTag: Edit => Deleting tag {0} ...", ctvm.TagId);
                            DbContext.Remove<ChargeTag>(currentChargeTag);
                            DbContext.SaveChanges();
                            Logger.LogInformation("ChargeTag: Edit => charge tag deleted: {0}", ctvm.TagId);
                        }
                        else
                        {
                            // Save existing tag
                            Logger.LogDebug("ChargeTag: Edit => Saving tag {0} ...", ctvm.TagId);
                            currentChargeTag.TagName = ctvm.TagName;
                            currentChargeTag.ParentTagId = ctvm.ParentTagId;
                            currentChargeTag.ExpiryDate = ctvm.ExpiryDate;
                            currentChargeTag.Blocked = ctvm.Blocked;
                            DbContext.SaveChanges();
                            UpdateChargeTagUsers(currentChargeTag.TagId, ctvm.Users);
                            DbContext.SaveChanges();
                            Logger.LogInformation("ChargeTag: Edit => charge tag saved: {0} / {1}", ctvm.TagId, ctvm.TagName);
                        }
                    }

                    return RedirectToAction("ChargeTag", new { Id = "" });
                }
                else
                {
                    // List all charge tags
                    ctvm = new ChargeTagViewModel();
                    ctvm.ChargeTags = dbChargeTags;
                    ctvm.CurrentTagId = Id;
                    ctvm.Users = BuildUserAssignments(dbUsers, new HashSet<int>());

                    if (currentChargeTag != null)
                    {
                        ctvm.TagId = currentChargeTag.TagId;
                        ctvm.TagName = currentChargeTag.TagName;
                        ctvm.ParentTagId = currentChargeTag.ParentTagId;
                        ctvm.ExpiryDate = currentChargeTag.ExpiryDate;
                        ctvm.Blocked = (currentChargeTag.Blocked != null) && currentChargeTag.Blocked.Value;

                        HashSet<int> assignedUserIds = DbContext.UserChargeTags
                            .Include(tag => tag.User)
                            .Where(tag => tag.TagId == currentChargeTag.TagId)
                            .Select(tag => tag.UserId)
                            .ToHashSet();
                        ctvm.Users = BuildUserAssignments(dbUsers, assignedUserIds);
                    }

                    string viewName = (!string.IsNullOrEmpty(ctvm.TagId) || Id=="@") ? "ChargeTagDetail" : "ChargeTagList";
                    return View(viewName, ctvm);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargeTag: Error loading oder saving charge tag(s) from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        private List<ChargeTagUserAssignmentViewModel> BuildUserAssignments(IEnumerable<User> users, IEnumerable<ChargeTagUserAssignmentViewModel> selectedAssignments)
        {
            HashSet<int> assignedUserIds = selectedAssignments == null
                ? new HashSet<int>()
                : selectedAssignments.Where(user => user.IsAssigned).Select(user => user.UserId).ToHashSet();

            return BuildUserAssignments(users, assignedUserIds);
        }

        private List<ChargeTagUserAssignmentViewModel> BuildUserAssignments(IEnumerable<User> users, HashSet<int> assignedUserIds)
        {
            List<ChargeTagUserAssignmentViewModel> assignments = new List<ChargeTagUserAssignmentViewModel>();

            foreach (User user in users)
            {
                assignments.Add(new ChargeTagUserAssignmentViewModel
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    IsAssigned = assignedUserIds.Contains(user.UserId)
                });
            }

            return assignments;
        }

        private void UpdateChargeTagUsers(string tagId, IEnumerable<ChargeTagUserAssignmentViewModel> assignments)
        {
            if (string.IsNullOrWhiteSpace(tagId) || assignments == null)
            {
                return;
            }

            HashSet<int> assignedUserIds = assignments
                .Where(user => user.IsAssigned)
                .Select(user => user.UserId)
                .ToHashSet();

            List<UserChargeTag> existingAssignments = DbContext.UserChargeTags
                .Where(tag => tag.TagId == tagId)
                .ToList();

            foreach (UserChargeTag assignment in existingAssignments)
            {
                if (!assignedUserIds.Contains(assignment.UserId))
                {
                    DbContext.UserChargeTags.Remove(assignment);
                }
            }

            foreach (int userId in assignedUserIds)
            {
                if (!existingAssignments.Any(tag => tag.UserId == userId))
                {
                    DbContext.UserChargeTags.Add(new UserChargeTag
                    {
                        UserId = userId,
                        TagId = tagId
                    });
                }
            }
        }
    }
}
