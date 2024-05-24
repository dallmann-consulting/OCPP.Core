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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        const char CSV_Seperator = ';';

        [Authorize]
        public IActionResult Export(string Id, string ConnectorId)
        {
            Logger.LogTrace("Export: Loading charge point transactions...");

            int currentConnectorId = -1;
            int.TryParse(ConnectorId, out currentConnectorId);

            TransactionListViewModel tlvm = new TransactionListViewModel();
            tlvm.CurrentChargePointId = Id;
            tlvm.CurrentConnectorId = currentConnectorId;
            tlvm.ConnectorStatuses = new List<ConnectorStatus>();
            tlvm.Transactions = new List<Transaction>();

            try
            {
                string ts = Request.Query["t"];
                int days = 30;
                if (ts == "2")
                {
                    // 90 days
                    days = 90;
                    tlvm.Timespan = 2;
                }
                else if (ts == "3")
                {
                    // 365 days
                    days = 365;
                    tlvm.Timespan = 3;
                }
                else
                {
                    // 30 days
                    days = 30;
                    tlvm.Timespan = 1;
                }

                string currentConnectorName = string.Empty;

                Logger.LogTrace("Export: Loading charge points...");
                tlvm.ConnectorStatuses = DbContext.ConnectorStatuses.ToList<ConnectorStatus>();

                // Preferred: use specific connector name
                foreach (ConnectorStatus cs in tlvm.ConnectorStatuses)
                {
                    if (cs.ChargePointId == Id && cs.ConnectorId == currentConnectorId)
                    {
                        currentConnectorName = cs.ConnectorName;
                        /*
                        if (string.IsNullOrEmpty(tlvm.CurrentConnectorName))
                        {
                            currentConnectorName = $"{Id}:{cs.ConnectorId}";
                        }
                        */
                        break;
                    }
                }
                // default: combined name with charge point and connector
                if (string.IsNullOrEmpty(currentConnectorName))
                {
                    tlvm.ChargePoints = DbContext.ChargePoints.ToList<ChargePoint>();
                    foreach(ChargePoint cp in tlvm.ChargePoints)
                    {
                        if (cp.ChargePointId == Id)
                        {
                            currentConnectorName = $"{cp.Name}:{currentConnectorId}";
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(currentConnectorName))
                    {
                        // Fallback: ID + connector
                        currentConnectorName = $"{Id}:{currentConnectorId}";
                    }
                }

                // load charge tags for name resolution
                Logger.LogTrace("Export: Loading charge tags...");
                tlvm.ChargeTags = DbContext.ChargeTags.ToList<ChargeTag>();

                if (!string.IsNullOrEmpty(tlvm.CurrentChargePointId))
                {
                    Logger.LogTrace("Export: Loading charge point transactions...");
                    tlvm.Transactions = DbContext.Transactions
                                        .Where(t => t.ChargePointId == tlvm.CurrentChargePointId &&
                                                    t.ConnectorId == tlvm.CurrentConnectorId &&
                                                    t.StartTime >= DateTime.UtcNow.AddDays(-1 * days))
                                        .OrderByDescending(t => t.TransactionId)
                                        .ToList<Transaction>();
                }

                StringBuilder connectorName = new StringBuilder(currentConnectorName);
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    connectorName.Replace(c, '_');
                }

                string filename = string.Format("Transactions_{0}.csv", connectorName);
                string csv = CreateCsv(tlvm, currentConnectorName);
                Logger.LogInformation("Export: File => {0} Chars / Name '{1}'", csv.Length, filename);

                return File(Encoding.GetEncoding("ISO-8859-1").GetBytes(csv), "text/csv", filename);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Export: Error loading data from database");
            }

            return View(tlvm);
        }

        private string CreateCsv(TransactionListViewModel tlvm, string currentConnectorName)
        {
            StringBuilder csv = new StringBuilder(8192);
            csv.Append(EscapeCsvValue(_localizer["Connector"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["StartTime"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["StartTag"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["StartMeter"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["StopTime"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["StopTag"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["StopMeter"]));
            csv.Append(CSV_Seperator);
            csv.Append(EscapeCsvValue(_localizer["ChargeSum"]));

            if (tlvm != null && tlvm.Transactions != null)
            {
                foreach (Transaction t in tlvm.Transactions)
                {
                    csv.AppendLine();
                    csv.Append(EscapeCsvValue(currentConnectorName));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(t.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(t.StartTag.ToString()));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(string.Format("{0:0.0##}", t.MeterStart)));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(((t.StopTime != null) ? t.StopTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(t.StopTag.ToString()));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(((t.MeterStop != null) ? string.Format("{0:0.0##}", t.MeterStop) : string.Empty)));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(((t.MeterStop != null) ? string.Format("{0:0.0##}", (t.MeterStop - t.MeterStart)) : string.Empty)));
                }
            }

            return csv.ToString();
        }

        private string EscapeCsvValue(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (value.Contains(CSV_Seperator))
                {
                    if (value.Contains('"'))
                    {
                        // replace '"' with '""'
                        value.Replace("\"", "\"\"");
                    }

                    // put value in "
                    value = string.Format("\"{0}\"", value);
                }
            }
            return value;
        }
    }
}
