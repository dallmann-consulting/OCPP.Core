using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        public AccountController(
            UserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config) : base(userManager, loggerFactory, config)
        {
            Logger = loggerFactory.CreateLogger<AccountController>();
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(UserModel userModel, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                await UserManager.SignIn(this.HttpContext, userModel, false);
                if (userModel != null && !string.IsNullOrWhiteSpace(userModel.Username))
                {
                    Logger.LogInformation("User '{0}' logged in", userModel.Username);
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    Logger.LogInformation("Invalid login attempt: User '{0}'", userModel.Username);
                    ModelState.AddModelError(string.Empty, "Invalid login attempt");
                    return View(userModel);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(userModel);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Logout(UserModel userModel)
        {
            Logger.LogInformation("Signing our user '{0}'", userModel.Username);
            await UserManager.SignOut(this.HttpContext);

            return RedirectToAction(nameof(AccountController.Login), "Account");
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), Constants.HomeController);
            }
        }
    }
}
