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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Deletes old entries in the table MessageLog ("MessageLogRetentionDays", 0 = keep forever).
    /// Runs shortly after the start and then once a day.
    /// </summary>
    public class MessageLogCleanupService : BackgroundService
    {
        public const int DefaultRetentionDays = 90;

        // deleting in blocks => short locks and a small transaction log (SQL Server)
        internal const int BatchSize = 10000;

        // Delay for first start (after the server starts)
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);

        // Repeat interval
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MessageLogCleanupService> _logger;
        private readonly int _retentionDays;

        public MessageLogCleanupService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MessageLogCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _retentionDays = configuration.GetValue<int>("MessageLogRetentionDays", DefaultRetentionDays);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_retentionDays <= 0)
            {
                _logger.LogInformation("MessageLogCleanup => Disabled ('MessageLogRetentionDays' = {0})", _retentionDays);
                return;
            }
            _logger.LogInformation("MessageLogCleanup => Entries older than {0} days are deleted daily", _retentionDays);

            try
            {
                await Task.Delay(StartDelay, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using IServiceScope scope = _scopeFactory.CreateScope();
                        OCPPCoreContext dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();
                        int deleted = await DeleteOldEntriesAsync(dbContext, DateTime.UtcNow.AddDays(-_retentionDays), BatchSize, stoppingToken);
                        if (deleted > 0)
                        {
                            _logger.LogInformation("MessageLogCleanup => {0} entries older than {1} days deleted", deleted, _retentionDays);
                        }
                    }
                    catch (Exception exp) when (exp is not OperationCanceledException)
                    {
                        // e.g. database not available => next try in the next interval
                        _logger.LogError(exp, "MessageLogCleanup => Error deleting old entries: {0}", exp.Message);
                    }

                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
        }

        /// <summary>
        /// Deletes all entries before "olderThan" (UTC) in blocks and returns the number of deleted entries
        /// </summary>
        internal static async Task<int> DeleteOldEntriesAsync(OCPPCoreContext dbContext, DateTime olderThan, int batchSize, CancellationToken cancellationToken)
        {
            int total = 0;
            int deleted;
            do
            {
                IQueryable<int> batch = dbContext.MessageLogs
                    .Where(l => l.LogTime < olderThan)
                    .OrderBy(l => l.LogId)
                    .Select(l => l.LogId)
                    .Take(batchSize);
                deleted = await dbContext.MessageLogs
                    .Where(l => batch.Contains(l.LogId))
                    .ExecuteDeleteAsync(cancellationToken);
                total += deleted;
            }
            while (deleted >= batchSize && !cancellationToken.IsCancellationRequested);

            return total;
        }
    }
}
