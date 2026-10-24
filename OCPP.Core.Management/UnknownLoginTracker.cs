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
using Microsoft.Extensions.Caching.Memory;

namespace OCPP.Core.Management
{
    /// <summary>
    /// Counts failed logins with unknown user names - exactly like ASP.NET Identity does for existing accounts
    /// (counter, temporary lockout). An attacker can't distinguish unknown user names from existing accounts
    /// by the lockout message.
    /// </summary>
    public class UnknownLoginTracker
    {
        // limited size => many tried user names can't fill the memory
        private const int MaxEntries = 10000;
        // Identity keeps the counter until a successful login or a lockout => keep the entries long
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromHours(24);

        private readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = MaxEntries });
        private readonly object _lock = new object();

        private class Entry
        {
            public int FailedCount;
            public DateTimeOffset? LockoutEnd;
        }

        /// <summary>
        /// End of the current lockout of the (unknown) user name or null if not locked
        /// </summary>
        public DateTimeOffset? GetLockoutEnd(string normalizedUserName)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(Key(normalizedUserName), out Entry entry) &&
                    entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > DateTimeOffset.UtcNow)
                {
                    return entry.LockoutEnd;
                }
                return null;
            }
        }

        /// <summary>
        /// Registers a failed login. Returns the end of the lockout if the user name is locked now (otherwise null).
        /// Same behavior as Identity: after "maxFailedAttempts" failures => lockout, counter starts again at 0.
        /// </summary>
        public DateTimeOffset? RegisterFailure(string normalizedUserName, int maxFailedAttempts, TimeSpan lockoutTime)
        {
            lock (_lock)
            {
                string key = Key(normalizedUserName);
                if (!_cache.TryGetValue(key, out Entry entry))
                {
                    entry = new Entry();
                }

                entry.FailedCount++;
                if (entry.FailedCount >= maxFailedAttempts)
                {
                    entry.FailedCount = 0;
                    entry.LockoutEnd = DateTimeOffset.UtcNow.Add(lockoutTime);
                }

                _cache.Set(key, entry, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = EntryLifetime });

                return (entry.LockoutEnd.HasValue && entry.LockoutEnd.Value > DateTimeOffset.UtcNow) ? entry.LockoutEnd : null;
            }
        }

        private static string Key(string normalizedUserName) => normalizedUserName ?? string.Empty;
    }
}
