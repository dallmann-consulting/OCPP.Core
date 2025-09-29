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

using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCPP.Core.Management
{
    /// <summary>
    /// User Manager for Login/Logout
    /// </summary>
    public class UserManager : IUserManager
    {
        private IConfiguration _configuration;
        private OCPPCoreContext _dbContext;

        public UserManager(IConfiguration configuration, OCPPCoreContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public async Task SignIn(HttpContext httpContext, UserModel user, bool isPersistent = false)
        {
            try
            {
                IEnumerable cfgUsers = _configuration.GetSection("Users").GetChildren();

                bool success = false;
                foreach (ConfigurationSection cfgUser in cfgUsers)
                {
                    if (cfgUser.GetValue<string>("Username") == user.Username &&
                        cfgUser.GetValue<string>("Password") == user.Password)
                    {
                        user.IsAdmin = cfgUser.GetValue<bool>(Constants.AdminRoleName);
                        ClaimsIdentity identity = new ClaimsIdentity(this.GetUserClaims(user), CookieAuthenticationDefaults.AuthenticationScheme);
                        ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                        AuthenticationProperties authProperties = new AuthenticationProperties
                        {
                            IsPersistent = true, // Persist the cookie after the browser is closed
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(72)
                        };

                        success = true;
                        WriteMessageLog("Login", $"Success - User '{user.Username}'");

                        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
                        break;
                    }
                }

                if (!success)
                {
                    WriteMessageLog("Login", $"Failure - User '{user.Username}'");
                }
            }
            catch //(Exception exp)
            {
            }
        }

        public async Task SignOut(HttpContext httpContext)
        {
            WriteMessageLog("Logout", $"User '{httpContext.User?.Identity?.Name}'");
            await httpContext.SignOutAsync();
        }

        private IEnumerable<Claim> GetUserClaims(UserModel user)
        {
            List<Claim> claims = new List<Claim>();

            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Username));
            claims.Add(new Claim(ClaimTypes.Name, user.Username));
            claims.AddRange(this.GetUserRoleClaims(user));
            return claims;
        }

        private IEnumerable<Claim> GetUserRoleClaims(UserModel user)
        {
            List<Claim> claims = new List<Claim>();

            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Username));
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, Constants.AdminRoleName));
            }
            return claims;
        }

        private void WriteMessageLog(string message, string result)
        {
            try
            {
                MessageLog msgLog = new MessageLog();
                msgLog.ChargePointId = "UserManager";
                msgLog.LogTime = DateTime.UtcNow;
                msgLog.Message = message;
                msgLog.Result = result;
                _dbContext.MessageLogs.Add(msgLog);
                _dbContext.SaveChanges();
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Interface for User Manager
    /// </summary>
    public interface IUserManager
    {
        Task SignIn(HttpContext httpContext, UserModel user, bool isPersistent);

        Task SignOut(HttpContext httpContext);
    }
}