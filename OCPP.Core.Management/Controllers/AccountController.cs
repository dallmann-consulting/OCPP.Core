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

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        private readonly UserManager<IdentityUser> _identityUserManager;

        public AccountController(
            IUserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext,
            UserManager<IdentityUser> identityUserManager)
            : base(userManager, loggerFactory, config, dbContext)
        {
            Logger = loggerFactory.CreateLogger<AccountController>();
            _identityUserManager = identityUserManager;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UserModel userModel, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                bool success = await UserManager.SignIn(this.HttpContext, userModel, false);
                if (success)
                    return RedirectToLocal(returnUrl);

                ModelState.AddModelError(string.Empty, "Invalid login attempt");
            }
            return View(userModel);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Logout(UserModel userModel)
        {
            await UserManager.SignOut(this.HttpContext);
            return RedirectToAction(nameof(AccountController.Login), "Account");
        }

        // GET /Account/Setup — only accessible in first-run mode
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Setup()
        {
            if (!ApplicationState.IsFirstRun)
                return NotFound();

            return View();
        }

        // POST /Account/Setup
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Setup(SetupModel model)
        {
            if (!ApplicationState.IsFirstRun)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var user = new IdentityUser { UserName = model.Username, SecurityStamp = System.Guid.NewGuid().ToString() };
            var result = await _identityUserManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // First user is always admin
            var roleManager = HttpContext.RequestServices
                .GetService(typeof(RoleManager<IdentityRole>)) as RoleManager<IdentityRole>;
            if (roleManager != null && !await roleManager.RoleExistsAsync(Constants.AdminRoleName))
                await roleManager.CreateAsync(new IdentityRole(Constants.AdminRoleName));

            await _identityUserManager.AddToRoleAsync(user, Constants.AdminRoleName);

            ApplicationState.IsFirstRun = false;

            Logger.LogInformation("First-run setup complete. Admin user '{Username}' created.", model.Username);

            return RedirectToAction(nameof(Login), "Account");
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(HomeController.Index), Constants.HomeController);
        }
    }
}
