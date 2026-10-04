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
using System.Linq;
using System.Threading.Tasks;
using OCPP.Core.Server.Messages_Api;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Collects the parts (NotifyReport messages) of a requested OCPP 2.x report (GetBaseReport)
    /// </summary>
    public class PendingReport
    {
        private readonly object _lock = new object();
        private readonly SortedDictionary<int, List<ApiVariableResult>> _parts = new SortedDictionary<int, List<ApiVariableResult>>();
        private readonly int _maxItems;
        private int? _lastSeqNo;
        private int _itemCount;

        public PendingReport(int requestId, int maxItems)
        {
            RequestId = requestId;
            _maxItems = maxItems;
            LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Request ID of the GetBaseReport request
        /// </summary>
        public int RequestId { get; }

        /// <summary>
        /// Time of the last received part (or creation)
        /// </summary>
        public DateTime LastActivity { get; private set; }

        /// <summary>
        /// Completed when all parts are received (result null) or the report was aborted (result = ApiReportStatus)
        /// </summary>
        public TaskCompletionSource<string> Completed { get; } = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Adds a received report part
        /// </summary>
        public void AddPart(int seqNo, bool tbc, List<ApiVariableResult> items)
        {
            lock (_lock)
            {
                if (Completed.Task.IsCompleted) return;

                LastActivity = DateTime.UtcNow;
                if (!_parts.ContainsKey(seqNo))
                {
                    // ignore repeated parts
                    _parts.Add(seqNo, items);
                    _itemCount += items.Count;
                }
                if (!tbc)
                {
                    _lastSeqNo = seqNo;
                }

                if (_maxItems > 0 && _itemCount > _maxItems)
                {
                    Completed.TrySetResult(ApiReportStatus.TooLarge);
                }
                else if (_lastSeqNo.HasValue && MissingSeqNo().Count == 0)
                {
                    Completed.TrySetResult(null);
                }
            }
        }

        /// <summary>
        /// Aborts the report (e.g. chargepoint disconnected)
        /// </summary>
        public void Abort(string status)
        {
            Completed.TrySetResult(status);
        }

        /// <summary>
        /// Creates the API response with all received parts (in order of the sequence numbers)
        /// </summary>
        public ApiVariablesResponse CreateResponse(string status)
        {
            lock (_lock)
            {
                ApiVariablesResponse response = new ApiVariablesResponse();
                foreach (List<ApiVariableResult> items in _parts.Values)
                {
                    response.Variables.AddRange(items);
                }

                List<int> missing = MissingSeqNo();
                if (status == null && _parts.Count == 0)
                {
                    status = ApiReportStatus.Timeout;
                }
                else if (status == null && (missing.Count > 0 || !_lastSeqNo.HasValue))
                {
                    status = ApiReportStatus.Incomplete;
                }
                response.Status = status;
                if (missing.Count > 0)
                {
                    response.MissingSeqNo = missing;
                }
                if (status != null && !_lastSeqNo.HasValue && _parts.Count > 0)
                {
                    response.StatusInfo = $"Received {_parts.Count} parts - last part (tbc=false) is missing";
                }
                return response;
            }
        }

        /// <summary>
        /// Missing sequence numbers up to the highest known sequence number
        /// </summary>
        private List<int> MissingSeqNo()
        {
            int maxSeqNo = _lastSeqNo ?? (_parts.Count > 0 ? _parts.Keys.Max() : -1);
            return Enumerable.Range(0, maxSeqNo + 1).Where(s => !_parts.ContainsKey(s)).ToList();
        }
    }
}
