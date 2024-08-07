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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml.InkML;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        private const char DefaultCSVSeparator = ';';

        [Authorize]
        public IActionResult Export(string Id, string ConnectorId)
        {
            try
            {
                var tlvm = LoadTransactionListViewModel(Id, ConnectorId);
                var workbook = CreateSpreadsheet(tlvm);

                using var memoryStream = new MemoryStream();
                IEnumerable<string> lines = workbook.Worksheet(1).RowsUsed().Select(row =>
                    string.Join(DefaultCSVSeparator, row.Cells(1, row.LastCellUsed(XLCellsUsedOptions.AllContents).Address.ColumnNumber)
                                                    .Select(cell => EscapeCsvValue(cell.GetValue<string>(), DefaultCSVSeparator))));

                using (var writer = new StreamWriter(memoryStream, Encoding.GetEncoding("ISO-8859-1"), 4096, true))
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }

                memoryStream.Position = 0;
                return File(memoryStream.ToArray(), "text/csv", $"Transactions_{SanitizeFileName(tlvm.CurrentConnectorName)}.csv");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Export CSV: Error loading data from database");
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        public IActionResult ExportXlsx(string Id, string ConnectorId)
        {
            try
            {
                var tlvm = LoadTransactionListViewModel(Id, ConnectorId);
                var workbook = CreateSpreadsheet(tlvm);

                using var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);
                memoryStream.Position = 0;

                return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Transactions_{SanitizeFileName(tlvm.CurrentConnectorName)}.xlsx");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Export XLSX: Error loading data from database");
                return StatusCode(500, "Internal server error");
            }
        }

        private TransactionListViewModel LoadTransactionListViewModel(string Id, string ConnectorId)
        {
            Logger.LogTrace("Export: Loading charge point transactions...");

            if (!int.TryParse(ConnectorId, out int currentConnectorId))
            {
                currentConnectorId = -1;
            }

            var tlvm = new TransactionListViewModel
            {
                CurrentChargePointId = Id,
                CurrentConnectorId = currentConnectorId,
                ConnectorStatuses = new List<ConnectorStatus>(),
                Transactions = new List<TransactionExtended>()
            };

            string ts = Request.Query["t"];
            int days = ts switch
            {
                "2" => 90,
                "3" => 365,
                _ => 30,
            };
            tlvm.Timespan = ts switch
            {
                "2" => 2,
                "3" => 3,
                _ => 1,
            };

            Logger.LogTrace("Export: Loading charge points and connectors...");
            tlvm.ConnectorStatuses = DbContext.ConnectorStatuses.Include(cs => cs.ChargePoint).ToList();

            tlvm.CurrentConnectorName = tlvm.ConnectorStatuses
                .FirstOrDefault(cs => cs.ChargePointId == Id && cs.ConnectorId == currentConnectorId)?.ToString() ?? $"{Id}:{currentConnectorId}";

            if (!string.IsNullOrEmpty(tlvm.CurrentChargePointId))
            {
                Logger.LogTrace("Export: Loading charge point transactions...");
                tlvm.Transactions = (from t in DbContext.Transactions
                                      join startCT in DbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                                      from startCT in ft_tmp.DefaultIfEmpty()
                                      join stopCT in DbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                                      from stopCT in ft.DefaultIfEmpty()
                                      where (t.ChargePointId == tlvm.CurrentChargePointId &&
                                                t.ConnectorId == tlvm.CurrentConnectorId &&
                                                t.StartTime >= DateTime.UtcNow.AddDays(-1 * days))
                                     select new TransactionExtended
                                      {
                                          TransactionId = t.TransactionId,
                                          Uid = t.Uid,
                                          ChargePointId = t.ChargePointId,
                                          ConnectorId = t.ConnectorId,
                                          StartTagId = t.StartTagId,
                                          StartTime = t.StartTime,
                                          MeterStart = t.MeterStart,
                                          StartResult = t.StartResult,
                                          StopTagId = t.StopTagId,
                                          StopTime = t.StopTime,
                                          MeterStop = t.MeterStop,
                                          StopReason = t.StopReason,
                                          StartTagName = startCT.TagName,
                                          StartTagParentId = startCT.ParentTagId,
                                          StopTagName = stopCT.TagName,
                                          StopTagParentId = stopCT.ParentTagId
                                      })
                                        .OrderByDescending(t => t.TransactionId)
                                        .AsNoTracking()
                                        .ToList();
            }

            return tlvm;
        }

        private XLWorkbook CreateSpreadsheet(TransactionListViewModel tlvm)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Transactions");

            worksheet.Cell(1, 1).Value = _localizer["Connector"].ToString();
            worksheet.Cell(1, 2).Value = _localizer["StartTime"].ToString();
            worksheet.Cell(1, 3).Value = _localizer["StartTag"].ToString();
            worksheet.Cell(1, 4).Value = _localizer["StartMeter"].ToString();
            worksheet.Cell(1, 5).Value = _localizer["StopTime"].ToString();
            worksheet.Cell(1, 6).Value = _localizer["StopTag"].ToString();
            worksheet.Cell(1, 7).Value = _localizer["StopMeter"].ToString();
            worksheet.Cell(1, 8).Value = _localizer["ChargeSum"].ToString();

            if (tlvm?.Transactions != null)
            {
                int row = 2;
                foreach (TransactionExtended t in tlvm?.Transactions)
                {
                    worksheet.Cell(row, 1).Value = tlvm.CurrentConnectorName;
                    worksheet.Cell(row, 2).SetValue(t.StartTime.ToLocalTime());
                    worksheet.Cell(row, 3).Value = string.IsNullOrEmpty(t.StartTagName) ? t.StartTagId : t.StartTagName;
                    worksheet.Cell(row, 4).SetValue(t.MeterStart);
                    if (t.StopTime.HasValue)
                        worksheet.Cell(row, 5).SetValue(t.StopTime?.ToLocalTime());
                    worksheet.Cell(row, 6).Value = string.IsNullOrEmpty(t.StopTagName) ? t.StopTagId : t.StopTagName;
                    if (t.MeterStop.HasValue)
                    {
                        worksheet.Cell(row, 7).SetValue(t.MeterStop);
                        worksheet.Cell(row, 8).SetValue(t.MeterStop - t.MeterStart);
                    }

                    row++;
                }
            }

            worksheet.Columns().AdjustToContents();

            return workbook;
        }

        private string EscapeCsvValue(string value, char separator)
        {
            if (!string.IsNullOrEmpty(value) && (value.Contains(separator) || value.Contains('"')))
            {
                value = $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedFileName = new StringBuilder(fileName);
            foreach (var c in invalidChars)
            {
                sanitizedFileName.Replace(c, '_');
            }
            return sanitizedFileName.ToString();
        }
    }
}
