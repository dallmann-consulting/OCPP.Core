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
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

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

        /// <summary>
        /// Header with the logged in user for commands to the OCPP server => EventLog of the charge point
        /// (URL encoded: HTTP headers only allow ASCII)
        /// </summary>
        protected void AddUserHeader(HttpClient httpClient)
        {
            string user = User?.Identity?.Name;
            if (!string.IsNullOrEmpty(user))
            {
                httpClient.DefaultRequestHeaders.Add("X-OCPP-User", Uri.EscapeDataString(user));
            }
        }
    }
}
