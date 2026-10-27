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
    /// Deletes old entries in the tables MessageLog ("MessageLogRetentionDays") and EventLog ("EventLogRetentionDays"), 0 = keep forever.
    /// Runs shortly after the start and then once a day.
    /// </summary>
    public class LogCleanupService : BackgroundService
    {
        public const int DefaultMessageLogRetentionDays = 90;
        public const int DefaultEventLogRetentionDays = 365;

        // deleting in blocks => short locks and a small transaction log (SQL Server)
        internal const int BatchSize = 10000;

        // Delay for first start (after the server starts)
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(1);

        // Repeat interval
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogCleanupService> _logger;
        private readonly int _messageLogRetentionDays;
        private readonly int _eventLogRetentionDays;

        public LogCleanupService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<LogCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _messageLogRetentionDays = configuration.GetValue<int>("MessageLogRetentionDays", DefaultMessageLogRetentionDays);
            _eventLogRetentionDays = configuration.GetValue<int>("EventLogRetentionDays", DefaultEventLogRetentionDays);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LogSetting("MessageLog", "MessageLogRetentionDays", _messageLogRetentionDays);
            LogSetting("EventLog", "EventLogRetentionDays", _eventLogRetentionDays);
            if (_messageLogRetentionDays <= 0 && _eventLogRetentionDays <= 0) return;

            try
            {
                await Task.Delay(StartDelay, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_messageLogRetentionDays > 0)
                    {
                        await CleanupAsync("MessageLog", _messageLogRetentionDays, DeleteOldMessageLogEntriesAsync, stoppingToken);
                    }
                    if (_eventLogRetentionDays > 0)
                    {
                        await CleanupAsync("EventLog", _eventLogRetentionDays, DeleteOldEventsAsync, stoppingToken);
                    }

                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
        }

        private void LogSetting(string table, string setting, int retentionDays)
        {
            if (retentionDays <= 0)
            {
                _logger.LogInformation("LogCleanup => {0}: disabled ('{1}' = {2})", table, setting, retentionDays);
            }
            else
            {
                _logger.LogInformation("LogCleanup => {0}: entries older than {1} days are deleted daily", table, retentionDays);
            }
        }

        private async Task CleanupAsync(string table, int retentionDays,
            Func<OCPPCoreContext, DateTime, int, CancellationToken, Task<int>> delete, CancellationToken stoppingToken)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                OCPPCoreContext dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();
                int deleted = await delete(dbContext, DateTime.UtcNow.AddDays(-retentionDays), BatchSize, stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("LogCleanup => {0}: {1} entries older than {2} days deleted", table, deleted, retentionDays);
                }
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                // e.g. database not available => next try in the next interval
                _logger.LogError(exp, "LogCleanup => {0}: error deleting old entries: {1}", table, exp.Message);
            }
        }

        /// <summary>
        /// MessageLog: deletes all entries before "olderThan" (UTC) in blocks and returns the number of deleted entries
        /// </summary>
        internal static async Task<int> DeleteOldMessageLogEntriesAsync(OCPPCoreContext dbContext, DateTime olderThan, int batchSize, CancellationToken cancellationToken)
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

        /// <summary>
        /// EventLog: deletes all events before "olderThan" (UTC) in blocks and returns the number of deleted events
        /// </summary>
        internal static async Task<int> DeleteOldEventsAsync(OCPPCoreContext dbContext, DateTime olderThan, int batchSize, CancellationToken cancellationToken)
        {
            int total = 0;
            int deleted;
            do
            {
                IQueryable<long> batch = dbContext.ChargePointEvents
                    .Where(e => e.EventTime < olderThan)
                    .OrderBy(e => e.EventId)
                    .Select(e => e.EventId)
                    .Take(batchSize);
                deleted = await dbContext.ChargePointEvents
                    .Where(e => batch.Contains(e.EventId))
                    .ExecuteDeleteAsync(cancellationToken);
                total += deleted;
            }
            while (deleted >= batchSize && !cancellationToken.IsCancellationRequested);

            return total;
        }
    }
}
