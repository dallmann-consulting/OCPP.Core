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

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    [Authorize(Roles = Constants.AdminRoleName)]
    public class UserAdminController : BaseController
    {
        private readonly UserManager<IdentityUser> _identityUserManager;
        private readonly IStringLocalizer<UserAdminController> _localizer;

        public UserAdminController(
            IUserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext,
            UserManager<IdentityUser> identityUserManager,
            IStringLocalizer<UserAdminController> localizer)
            : base(userManager, loggerFactory, config, dbContext)
        {
            Logger = loggerFactory.CreateLogger<UserAdminController>();
            _identityUserManager = identityUserManager;
            _localizer = localizer;
        }

        // GET /UserAdmin
        public async Task<IActionResult> Index()
        {
            var users = await _identityUserManager.Users.ToListAsync();
            var model = new UserAdminListModel();
            foreach (var u in users.OrderBy(u => u.UserName))
            {
                model.Users.Add(new UserAdminItem
                {
                    Id = u.Id,
                    Username = u.UserName,
                    IsAdmin = await _identityUserManager.IsInRoleAsync(u, Constants.AdminRoleName)
                });
            }
            return View(model);
        }

        // GET /UserAdmin/Create
        public IActionResult Create() => View(new CreateUserModel());

        // POST /UserAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new IdentityUser { UserName = model.Username, SecurityStamp = System.Guid.NewGuid().ToString() };
            var result = await _identityUserManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return View(model);
            }

            if (model.IsAdmin)
                await _identityUserManager.AddToRoleAsync(user, Constants.AdminRoleName);

            Logger.LogInformation("Admin '{Admin}' created user '{Username}'",
                User.Identity.Name, model.Username);
            return RedirectToAction(nameof(Index));
        }

        // GET /UserAdmin/Edit/id
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _identityUserManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(new EditUserModel
            {
                Id = user.Id,
                Username = user.UserName,
                IsAdmin = await _identityUserManager.IsInRoleAsync(user, Constants.AdminRoleName)
            });
        }

        // POST /UserAdmin/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _identityUserManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // Update role
            bool isCurrentlyAdmin = await _identityUserManager.IsInRoleAsync(user, Constants.AdminRoleName);
            if (model.IsAdmin && !isCurrentlyAdmin)
                await _identityUserManager.AddToRoleAsync(user, Constants.AdminRoleName);
            else if (!model.IsAdmin && isCurrentlyAdmin)
                await _identityUserManager.RemoveFromRoleAsync(user, Constants.AdminRoleName);

            // Change password if provided
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _identityUserManager.GeneratePasswordResetTokenAsync(user);
                var result = await _identityUserManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!result.Succeeded)
                {
                    foreach (var e in result.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);
                    return View(model);
                }
            }

            Logger.LogInformation("Admin '{Admin}' edited user '{Username}'",
                User.Identity.Name, user.UserName);
            return RedirectToAction(nameof(Index));
        }

        // POST /UserAdmin/Delete/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            // Prevent deleting yourself
            var currentUser = await _identityUserManager.GetUserAsync(User);
            if (currentUser?.Id == id)
            {
                TempData["Error"] = _localizer["ErrorDeleteSelf"].Value;
                return RedirectToAction(nameof(Index));
            }

            var user = await _identityUserManager.FindByIdAsync(id);
            if (user != null)
            {
                Logger.LogInformation("Admin '{Admin}' deleted user '{Username}'",
                    User.Identity.Name, user.UserName);
                await _identityUserManager.DeleteAsync(user);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
