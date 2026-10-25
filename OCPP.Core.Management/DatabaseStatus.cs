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
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;

namespace OCPP.Core.Management
{
    /// <summary>
    /// Migration state of the database for the admin warning in the layout.
    /// The result is cached for a short time => no database query on every page view.
    /// </summary>
    public class DatabaseStatus
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DatabaseStatus> _logger;
        private readonly object _lock = new object();

        private List<string> _pendingMigrations = new List<string>();
        private DateTime _checked = DateTime.MinValue;

        public DatabaseStatus(IServiceScopeFactory scopeFactory, ILogger<DatabaseStatus> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Names of the pending migrations (empty list = database is up to date or state unknown)
        /// </summary>
        public IReadOnlyList<string> GetPendingMigrations()
        {
            lock (_lock)
            {
                if (DateTime.UtcNow - _checked > CacheDuration)
                {
                    try
                    {
                        using IServiceScope scope = _scopeFactory.CreateScope();
                        OCPPCoreContext dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();
                        _pendingMigrations = dbContext.GetPendingMigrationNames();
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "DatabaseStatus => Error checking the migration state: {0}", exp.Message);
                        _pendingMigrations = new List<string>();
                    }
                    _checked = DateTime.UtcNow;
                }
                return _pendingMigrations;
            }
        }
    }
}
