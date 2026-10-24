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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Validation;
using OCPP.Core.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OCPP.Core.Server
{
    public partial class ControllerBase
    {
        /// <summary>
        /// Internal string for OCPP protocol version
        /// </summary>
        protected virtual string ProtocolVersion { get;  }

        /// <summary>
        /// Configuration context for reading app settings
        /// </summary>
        protected IConfiguration Configuration { get; set; }

        /// <summary>
        /// Chargepoint status
        /// </summary>
        protected ChargePointStatus ChargePointStatus { get; set; }

        /// <summary>
        /// ILogger object
        /// </summary>
        protected ILogger Logger { get; set; }

        /// <summary>
        /// DbContext object
        /// </summary>
        protected OCPPCoreContext DbContext { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ControllerBase(IConfiguration config, ILoggerFactory loggerFactory, ChargePointStatus chargePointStatus, OCPPCoreContext dbContext)
        {
            Configuration = config;

            if (chargePointStatus != null)
            {
                ChargePointStatus = chargePointStatus;
            }
            else
            {
                Logger.LogError("New ControllerBase => empty chargepoint status");
            }
            DbContext = dbContext;
        }

        /// <summary>
        /// Parsed JSON schemas (key = schema file path, value = null if the file doesn't exist)
        /// => every schema file is loaded and parsed only once
        /// </summary>
        private static readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new ConcurrentDictionary<string, JsonSchema>();

        /// <summary>
        /// Deserialize and validate JSON message (if schema file exists)
        /// </summary>
        protected T DeserializeMessage<T>(OCPPMessage msg)
        {
            bool validateMessages = Configuration.GetValue<bool>("ValidateMessages", false);
            if (validateMessages)
            {
                JsonSchema schema = GetSchema(typeof(T).Name);
                if (schema != null)
                {
                    ICollection<ValidationError> errors = schema.Validate(msg.JsonPayload);
                    if (errors.Count > 0)
                    {
                        foreach (ValidationError err in errors)
                        {
                            Logger.LogWarning("DeserializeMessage {0} => Validation error: {1}", msg.Action, err);
                        }
                        throw new FormatException("Message validation failed");
                    }
                }
            }
            else
            {
                Logger.LogTrace("DeserializeMessage => Deserialization without schema validation");
            }

            using JsonTextReader reader = new JsonTextReader(new StringReader(msg.JsonPayload));
            JsonSerializer serializer = new JsonSerializer();
            return serializer.Deserialize<T>(reader);
        }

        /// <summary>
        /// Returns the (cached) JSON schema for a message type of this protocol version or null if no schema file exists
        /// </summary>
        private JsonSchema GetSchema(string msgTypeName)
        {
            string codeBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(codeBase)) return null;

            string filename = Path.Combine(codeBase, $"Schema{ProtocolVersion}", $"{msgTypeName}.json");
            return _schemaCache.GetOrAdd(filename, file =>
            {
                if (!File.Exists(file)) return null;

                Logger.LogTrace("DeserializeMessage => Loading schema file: {0}", file);
                return JsonSchema.FromJsonAsync(File.ReadAllText(file)).GetAwaiter().GetResult();
            });
        }


        /// <summary>
        /// Helper function for creating and updating the ConnectorStatus in then database
        /// </summary>
        protected bool UpdateConnectorStatus(int connectorId, string status, DateTimeOffset? statusTime, double? meter, DateTimeOffset? meterTime)
        {
            try
            {
                ConnectorStatus connectorStatus = DbContext.Find<ConnectorStatus>(ChargePointStatus.Id, connectorId);
                if (connectorStatus == null)
                {
                    // no matching entry => create connector status
                    connectorStatus = new ConnectorStatus();
                    connectorStatus.ChargePointId = ChargePointStatus.Id;
                    connectorStatus.ConnectorId = connectorId;
                    Logger.LogTrace("UpdateConnectorStatus => Creating new DB-ConnectorStatus: ID={0} / Connector={1}", connectorStatus.ChargePointId, connectorStatus.ConnectorId);
                    DbContext.Add<ConnectorStatus>(connectorStatus);
                    DbContext.SaveChanges();
                }

                if (!string.IsNullOrEmpty(status))
                {
                    DateTime dbTime = ((statusTime.HasValue) ? statusTime.Value : DateTimeOffset.UtcNow).DateTime;
                    DbContext.ConnectorStatuses.Where(cs => cs.ChargePointId == ChargePointStatus.Id && cs.ConnectorId == connectorId)
                        .ExecuteUpdate(s => s
                            .SetProperty(cs => cs.LastStatus, status)
                            .SetProperty(cs => cs.LastStatusTime, dbTime)
                            );
                }

                if (meter.HasValue)
                {
                    DateTime dbTime = ((meterTime.HasValue) ? meterTime.Value : DateTimeOffset.UtcNow).DateTime;
                    DbContext.ConnectorStatuses.Where(cs => cs.ChargePointId == ChargePointStatus.Id && cs.ConnectorId == connectorId)
                        .ExecuteUpdate(s => s
                            .SetProperty(cs => cs.LastMeter, meter.Value)
                            .SetProperty(cs => cs.LastMeterTime, dbTime)
                            );
                }
                Logger.LogInformation("UpdateConnectorStatus => Save ConnectorStatus: ID={0} / Connector={1} / Status={2} / Meter={3}", connectorStatus.ChargePointId, connectorId, status, meter);
                return true;
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "UpdateConnectorStatus => Exception writing connector status (ID={0} / Connector={1}): {2}", ChargePointStatus?.Id, connectorId, exp.Message);
            }

            return false;
        }

        /// <summary>
        /// Set/Update in memory connector status with meter (and more) values
        /// </summary>
        protected void UpdateMemoryConnectorStatus(int connectorId, double meterKWH, DateTimeOffset meterTime, double? currentChargeKW, double? stateOfCharge)
        {
            // Values <1 have no meaning => null
            if (currentChargeKW.HasValue && currentChargeKW < 0) currentChargeKW = null;
            if (stateOfCharge.HasValue && stateOfCharge < 0) stateOfCharge = null;

            OnlineConnectorStatus ocs = null;
            bool isNew = false;
            if (ChargePointStatus.OnlineConnectors.ContainsKey(connectorId))
            {
                ocs = ChargePointStatus.OnlineConnectors[connectorId];
            }
            else
            {
                ocs = new OnlineConnectorStatus();
                isNew = true; // append later when all values are correct
            }

            ocs.ChargeRateKW = currentChargeKW;
            if (meterKWH >= 0 && !currentChargeKW.HasValue &&
                ocs.MeterKWH.HasValue && ocs.MeterKWH <= meterKWH &&
                ocs.MeterValueDate < meterTime)
            {
                try
                {
                    // Chargepoint sends no power (kW) => calculate from meter and time (from last sample)
                    double diffMeter = meterKWH - ocs.MeterKWH.Value;
                    ocs.ChargeRateKW = diffMeter / ((meterTime.Subtract(ocs.MeterValueDate).TotalSeconds) / (60 * 60));
                    Logger.LogDebug("MeterValues => Calculated power for ChargePoint={0} / Connector={1} / Power: {2}kW", ChargePointStatus?.Id, connectorId, ocs.ChargeRateKW);
                }
                catch (Exception exp)
                {
                    Logger.LogWarning("MeterValues => Error calculating power for ChargePoint={0} / Connector={1}: {2}", ChargePointStatus?.Id, connectorId, exp.ToString());
                }
            }
            ocs.MeterKWH = meterKWH;
            ocs.MeterValueDate = meterTime;
            ocs.SoC = stateOfCharge;

            if (isNew)
            {
                if (ChargePointStatus.OnlineConnectors.TryAdd(connectorId, ocs))
                {
                    Logger.LogTrace("MeterValues => Set OnlineConnectorStatus for ChargePoint={0} / Connector={1} / meterKWH: {2}", ChargePointStatus?.Id, connectorId, meterKWH);
                }
                else
                {
                    Logger.LogError("MeterValues => Error adding new OnlineConnectorStatus for ChargePoint={0} / Connector={1} / meterKWH: {2}", ChargePointStatus?.Id, connectorId, meterKWH);
                }
            }
        }

        /// <summary>
        /// Finds an OCPP 2.x transaction of this charging station by its transaction id
        /// (the id is only unique per charging station - some stations simply count 1, 2, 3, ...)
        /// </summary>
        protected Transaction FindTransactionByUid(string uid)
        {
            return DbContext.Transactions
                .Where(t => t.ChargePointId == ChargePointStatus.Id && t.Uid == uid)
                .OrderByDescending(t => t.TransactionId)
                .FirstOrDefault();
        }

        /// <summary>
        /// Stores the current meter value of a transaction (OCPP 2.x "Updated"/"Ended").
        /// Without meter value in the message (meterKWH &lt; 0) the last value is kept.
        /// A transaction can start without meter value (MeterStart &lt; 0 = unknown)
        /// => the first valid meter value is used as start value.
        /// </summary>
        protected void SetTransactionMeter(Transaction transaction, double meterKWH)
        {
            if (meterKWH < 0) return;

            if (transaction.MeterStart < 0)
            {
                Logger.LogInformation("SetTransactionMeter => Transaction {0} started without meter value => start meter = {1} (kWh)", transaction.TransactionId, meterKWH);
                transaction.MeterStart = meterKWH;
            }
            transaction.MeterStop = meterKWH;
        }

        /// <summary>
        /// Closes open transactions on a connector when a new transaction starts there.
        /// A connector can only have one transaction at a time => the stop message of the old one got lost.
        /// Stop time and meter are estimated from the new transaction (marked with StopReason "Superseded").
        /// </summary>
        protected void CloseSupersededTransactions(int connectorId, DateTime newStartTime, double newMeterStart)
        {
            List<Transaction> openTransactions = DbContext.Transactions
                .Where(t => t.ChargePointId == ChargePointStatus.Id &&
                            t.ConnectorId == connectorId &&
                            !t.StopTime.HasValue &&
                            t.StartTime <= newStartTime)
                .ToList();

            foreach (Transaction transaction in openTransactions)
            {
                Logger.LogWarning("CloseSupersededTransactions => Closing open transaction {0} on chargepoint '{1}' / connector {2}: new transaction started (stop message lost?)", transaction.TransactionId, ChargePointStatus.Id, connectorId);
                transaction.StopTime = newStartTime;
                transaction.StopReason = TransactionStatus.StopReasonSuperseded;
                // last meter value from the transaction or the meter at the start of the new transaction
                if (!transaction.MeterStop.HasValue || transaction.MeterStop < transaction.MeterStart)
                {
                    SetTransactionMeter(transaction, newMeterStart);
                }
            }

            if (openTransactions.Count > 0)
            {
                DbContext.SaveChanges();
            }
        }

        /// <summary>
        /// Checks the tag that stopped a transaction: same tag as the start tag or a tag of the same group.
        /// Only for the log: the charging station decides about the stop (with Authorize and the group of the tag)
        /// and the CSMS cannot prevent a transaction from stopping.
        /// </summary>
        protected void CheckStopTag(Transaction transaction, string stopTagId, string stopTagGroup)
        {
            if (string.IsNullOrWhiteSpace(stopTagId) || string.IsNullOrEmpty(transaction.StartTagId))
            {
                // stopped without tag (EV/charging station) or transaction started without tag
                return;
            }
            if (transaction.StartTagId.Equals(stopTagId, StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            string startTagGroup = DbContext.Find<ChargeTag>(transaction.StartTagId)?.ParentTagId;
            if (!string.IsNullOrWhiteSpace(startTagGroup) &&
                string.Equals(startTagGroup, stopTagGroup, StringComparison.InvariantCultureIgnoreCase))
            {
                Logger.LogInformation("CheckStopTag => Transaction {0}: different tags but same group ('{1}')", transaction.TransactionId, startTagGroup);
                return;
            }

            Logger.LogWarning("CheckStopTag => Transaction {0} on chargepoint '{1}' was stopped with tag '{2}' which is neither the start tag '{3}' nor in its group => the charging station allowed the stop (configuration?)",
                transaction.TransactionId, ChargePointStatus.Id, stopTagId, transaction.StartTagId);
        }

        /// <summary>
        /// Clean charge tag Id from possible suffix ("..._abc")
        /// </summary>
        protected static string CleanChargeTagId(string rawChargeTagId, ILogger logger)
        {
            string idTag = rawChargeTagId;

            // KEBA adds the serial to the idTag ("<idTag>_<serial>") => cut off suffix
            if (!string.IsNullOrWhiteSpace(rawChargeTagId))
            {
                int sep = rawChargeTagId.IndexOf('_');
                if (sep >= 0)
                {
                    idTag = rawChargeTagId.Substring(0, sep);
                    logger.LogTrace("CleanChargeTagId => Charge tag '{0}' => '{1}'", rawChargeTagId, idTag);
                }
            }

            return idTag;
        }

        /// <summary>
        /// Return UtcNow + 1 year
        /// </summary>
        protected static DateTimeOffset MaxExpiryDate
        {
            get
            {
                return DateTime.UtcNow.Date.AddYears(1);
            }
        }
    }
}
