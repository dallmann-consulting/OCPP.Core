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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCPP.Core.Management
{
    /// <summary>
    /// User Manager for Login/Logout
    /// </summary>
    public class UserManager : IUserManager
    {
        private OCPPCoreContext _dbContext;

        public UserManager(OCPPCoreContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SignIn(HttpContext httpContext, UserModel user, bool isPersistent = false)
        {
            try
            {
                User dbUser = await _dbContext.Users.FirstOrDefaultAsync(dbUser => dbUser.Username == user.Username);
                if (dbUser != null && dbUser.Password == user.Password)
                {
                    user.UserId = dbUser.UserId;
                    user.IsAdmin = dbUser.IsAdmin;
                    ClaimsIdentity identity = new ClaimsIdentity(this.GetUserClaims(user), CookieAuthenticationDefaults.AuthenticationScheme);
                    ClaimsPrincipal principal = new ClaimsPrincipal(identity);

                    AuthenticationProperties authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true, // Persist the cookie after the browser is closed
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(72)
                    };

                    WriteMessageLog("Login", $"Success - User '{user.Username}'");

                    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
                }
                else
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

            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()));
            claims.Add(new Claim(ClaimTypes.Name, user.Username));
            claims.AddRange(this.GetUserRoleClaims(user));
            return claims;
        }

        private IEnumerable<Claim> GetUserRoleClaims(UserModel user)
        {
            List<Claim> claims = new List<Claim>();

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
