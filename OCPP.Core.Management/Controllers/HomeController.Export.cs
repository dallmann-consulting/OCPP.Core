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
        public IActionResult Export(string Id)
        {
            Logger.LogTrace("Export: Loading charge point transactions...");

            TransactionListViewModel tlvm = new TransactionListViewModel();
            tlvm.CurrentChargePointId = Id;
            tlvm.ChargePoints = new List<ChargePoint>();
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

                using (OCPPCoreContext dbContext = new OCPPCoreContext(this.Config))
                {
                    Logger.LogTrace("Export: Loading charge points...");
                    tlvm.ChargePoints = dbContext.ChargePoints.ToList<ChargePoint>();

                    foreach(ChargePoint cp in tlvm.ChargePoints)
                    {
                        if (cp.ChargePointId == Id)
                        {
                            tlvm.CurrentChargePointName = cp.Name;
                            if (string.IsNullOrEmpty(tlvm.CurrentChargePointName))
                            {
                                tlvm.CurrentChargePointName = Id;
                            }
                            break;
                        }
                    }

                    // load charge tags for name resolution
                    Logger.LogTrace("Export: Loading charge tags...");
                    tlvm.ChargePoints = dbContext.ChargePoints.ToList<ChargePoint>();
                    List<ChargeTag> chargeTags = dbContext.ChargeTags.ToList<ChargeTag>();
                    tlvm.ChargeTags = new Dictionary<string, ChargeTag>();
                    if (chargeTags != null)
                    {
                        foreach (ChargeTag tag in chargeTags)
                        {
                            tlvm.ChargeTags.Add(tag.TagId, tag);
                        }
                    }

                    if (!string.IsNullOrEmpty(tlvm.CurrentChargePointId))
                    {
                        Logger.LogTrace("Export: Loading charge point transactions...");
                        tlvm.Transactions = dbContext.Transactions
                                            .Where(t => t.ChargePointId == tlvm.CurrentChargePointId && t.StartTime >= DateTime.UtcNow.AddDays(-1 * days))
                                            .OrderByDescending(t => t.TransactionId)
                                            .ToList<Transaction>();
                    }

                    StringBuilder chargepointName = new StringBuilder(tlvm.CurrentChargePointName);
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        chargepointName.Replace(c, '_');
                    }

                    string filename = string.Format("Transactions_{0}.csv", chargepointName);
                    string csv = CreateCsv(tlvm);
                    Logger.LogInformation("Export: File => {0} Chars / Name '{1}'", csv.Length, filename);

                    return File(Encoding.GetEncoding("ISO-8859-1").GetBytes(csv), "text/csv", filename);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Export: Error loading charge points from database");
            }

            return View(tlvm);
        }

        private string CreateCsv(TransactionListViewModel tlvm)
        {
            StringBuilder csv = new StringBuilder(8192);
            csv.Append(EscapeCsvValue(_localizer["ChargePointId"]));
            csv.Append(CSV_Seperator);
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
                    string startTag = t.StartTagId;
                    string stopTag = t.StopTagId;
                    if (!string.IsNullOrEmpty(t.StartTagId) && tlvm.ChargeTags != null && tlvm.ChargeTags.ContainsKey(t.StartTagId))
                    {
                        startTag = tlvm.ChargeTags[t.StartTagId]?.TagName;
                    }
                    if (!string.IsNullOrEmpty(t.StopTagId) && tlvm.ChargeTags != null && tlvm.ChargeTags.ContainsKey(t.StopTagId))
                    {
                        stopTag = tlvm.ChargeTags[t.StopTagId]?.TagName;
                    }

                    csv.AppendLine();
                    csv.Append(EscapeCsvValue(t.ChargePointId));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(t.ConnectorId.ToString()));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(t.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(startTag));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(t.MeterStart.ToString()));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(((t.StopTime != null) ? t.StopTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty)));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(stopTag));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(((t.MeterStop != null) ? t.MeterStop.ToString() : string.Empty)));
                    csv.Append(CSV_Seperator);
                    csv.Append(EscapeCsvValue(((t.MeterStop != null) ? (t.MeterStop - t.MeterStart).ToString() : string.Empty)));
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
