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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System.Threading.Tasks;

namespace OCPP.Core.Management
{
    public class UserManager : IUserManager
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<UserManager> _logger;

        public UserManager(
            SignInManager<IdentityUser> signInManager,
            OCPPCoreContext dbContext,
            ILogger<UserManager> logger)
        {
            _signInManager = signInManager;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<bool> SignIn(HttpContext httpContext, UserModel user, bool isPersistent = false)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user.Username, user.Password, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("User '{Username}' logged in", user.Username);
                WriteMessageLog("Login", $"Success - User '{user.Username}'");
                return true;
            }

            _logger.LogInformation("Invalid login attempt for user '{Username}'", user.Username);
            WriteMessageLog("Login", $"Failure - User '{user.Username}'");
            return false;
        }

        public async Task SignOut(HttpContext httpContext)
        {
            WriteMessageLog("Logout", $"User '{httpContext.User?.Identity?.Name}'");
            await _signInManager.SignOutAsync();
        }

        private void WriteMessageLog(string message, string result)
        {
            try
            {
                var msgLog = new MessageLog
                {
                    ChargePointId = "UserManager",
                    LogTime = System.DateTime.UtcNow,
                    Message = message,
                    Result = result
                };
                _dbContext.MessageLogs.Add(msgLog);
                _dbContext.SaveChanges();
            }
            catch { }
        }
    }

    public interface IUserManager
    {
        Task<bool> SignIn(HttpContext httpContext, UserModel user, bool isPersistent);
        Task SignOut(HttpContext httpContext);
    }
}
