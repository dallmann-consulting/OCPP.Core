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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System.Security.Claims;

namespace OCPP.Core.Management.Controllers
{
    public class BaseController : Controller
    {
        protected IUserManager UserManager { get; private set; }

        protected ILogger Logger { get; set; }

        protected IConfiguration Config { get; private set; }

        protected OCPPCoreContext DbContext { get; private set; }

        public BaseController(
            IUserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext)
        {
            UserManager = userManager;
            Config = config;
            DbContext = dbContext;
        }

        protected int? GetCurrentUserId()
        {
            string userIdValue = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdValue, out int userId))
            {
                return userId;
            }

            return null;
        }

        protected HashSet<string> GetPermittedChargePointIds()
        {
            if (User != null && User.IsInRole(Constants.AdminRoleName))
            {
                return null;
            }

            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            }

            return DbContext.UserChargePoints
                .Where(point => point.UserId == userId.Value)
                .Select(point => point.ChargePointId)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        }

    }
}
