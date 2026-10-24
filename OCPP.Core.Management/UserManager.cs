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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System.Threading.Tasks;

namespace OCPP.Core.Management
{
    /// <summary>
    /// Result of a login attempt
    /// </summary>
    public class LoginResult
    {
        public bool Succeeded { get; private set; }

        /// <summary>
        /// Too many failed attempts => temporarily locked
        /// </summary>
        public bool IsLockedOut { get; private set; }

        /// <summary>
        /// Remaining minutes of the lockout (rounded up)
        /// </summary>
        public int LockoutMinutes { get; private set; }

        public static readonly LoginResult Success = new LoginResult { Succeeded = true };
        public static readonly LoginResult Failed = new LoginResult();

        public static LoginResult LockedOut(DateTimeOffset? lockoutEnd)
        {
            double minutes = lockoutEnd.HasValue ? (lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes : 1;
            return new LoginResult { IsLockedOut = true, LockoutMinutes = Math.Max(1, (int)Math.Ceiling(minutes)) };
        }
    }

    public class UserManager : IUserManager
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _identityUserManager;
        private readonly IPasswordHasher<IdentityUser> _passwordHasher;
        private readonly UnknownLoginTracker _unknownLoginTracker;
        private readonly LockoutOptions _lockoutOptions;
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<UserManager> _logger;

        // Hash for the dummy password check of unknown user names (same duration as for existing accounts)
        private static string _dummyPasswordHash;

        public UserManager(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> identityUserManager,
            IPasswordHasher<IdentityUser> passwordHasher,
            UnknownLoginTracker unknownLoginTracker,
            IOptions<IdentityOptions> identityOptions,
            OCPPCoreContext dbContext,
            ILogger<UserManager> logger)
        {
            _signInManager = signInManager;
            _identityUserManager = identityUserManager;
            _passwordHasher = passwordHasher;
            _unknownLoginTracker = unknownLoginTracker;
            _lockoutOptions = identityOptions.Value.Lockout;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<LoginResult> SignIn(HttpContext httpContext, UserModel user, bool isPersistent)
        {
            // MaxFailedAccessAttempts <= 0 => lockout deactivated
            bool lockoutActive = _lockoutOptions.MaxFailedAccessAttempts > 0;

            IdentityUser identityUser = string.IsNullOrEmpty(user.Username) ? null : await _identityUserManager.FindByNameAsync(user.Username);
            if (identityUser == null)
            {
                // Unknown user name => behave exactly like an existing account with a wrong password
                // (same messages, same lockout, same duration) => user names can't be found out systematically
                string normalizedName = _identityUserManager.NormalizeName(user.Username ?? string.Empty);
                DateTimeOffset? lockoutEnd = lockoutActive ? _unknownLoginTracker.GetLockoutEnd(normalizedName) : null;
                if (!lockoutEnd.HasValue)
                {
                    VerifyDummyPassword(user.Password);
                    if (lockoutActive)
                    {
                        lockoutEnd = _unknownLoginTracker.RegisterFailure(normalizedName, _lockoutOptions.MaxFailedAccessAttempts, _lockoutOptions.DefaultLockoutTimeSpan);
                    }
                }
                return LogFailure(user.Username, lockoutEnd);
            }

            SignInResult result = await _signInManager.PasswordSignInAsync(identityUser, user.Password ?? string.Empty, isPersistent, lockoutOnFailure: lockoutActive);
            if (result.Succeeded)
            {
                _logger.LogInformation("User '{Username}' logged in", user.Username);
                WriteMessageLog("Login", $"Success - User '{user.Username}'");
                return LoginResult.Success;
            }

            DateTimeOffset? accountLockoutEnd = result.IsLockedOut ? await _identityUserManager.GetLockoutEndDateAsync(identityUser) : null;
            return LogFailure(user.Username, accountLockoutEnd);
        }

        public async Task SignOut(HttpContext httpContext)
        {
            WriteMessageLog("Logout", $"User '{httpContext.User?.Identity?.Name}'");
            await _signInManager.SignOutAsync();
        }

        private LoginResult LogFailure(string username, DateTimeOffset? lockoutEnd)
        {
            if (lockoutEnd.HasValue)
            {
                _logger.LogWarning("Login for user '{Username}' is locked until {LockoutEnd} (too many failed attempts)", username, lockoutEnd);
                WriteMessageLog("Login", $"Locked - User '{username}'");
                return LoginResult.LockedOut(lockoutEnd);
            }

            _logger.LogInformation("Invalid login attempt for user '{Username}'", username);
            WriteMessageLog("Login", $"Failure - User '{username}'");
            return LoginResult.Failed;
        }

        /// <summary>
        /// Password check against a dummy hash: an unknown user name takes as long as an existing account
        /// (otherwise the existence of an account could be detected by the response time)
        /// </summary>
        private void VerifyDummyPassword(string password)
        {
            IdentityUser dummyUser = new IdentityUser();
            _dummyPasswordHash ??= _passwordHasher.HashPassword(dummyUser, Guid.NewGuid().ToString());
            _passwordHasher.VerifyHashedPassword(dummyUser, _dummyPasswordHash, password ?? string.Empty);
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
        Task<LoginResult> SignIn(HttpContext httpContext, UserModel user, bool isPersistent);
        Task SignOut(HttpContext httpContext);
    }
}
