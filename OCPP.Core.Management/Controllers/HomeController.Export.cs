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

                using (var writer = new StreamWriter(memoryStream, Encoding.GetEncoding("ISO-8859-1"), 1024, true))
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
                Transactions = new List<Transaction>()
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

            Logger.LogTrace("Export: Loading charge points...");
            tlvm.ConnectorStatuses = DbContext.ConnectorStatuses.Include(cs => cs.ChargePoint).ToList();

            tlvm.CurrentConnectorName = tlvm.ConnectorStatuses
                .FirstOrDefault(cs => cs.ChargePointId == Id && cs.ConnectorId == currentConnectorId)?.ToString() ?? $"{Id}:{currentConnectorId}";

            Logger.LogTrace("Export: Loading charge tags...");
            var chargeTags = DbContext.ChargeTags.ToList();
            tlvm.ChargeTags = chargeTags.ToDictionary(tag => tag.TagId);

            if (!string.IsNullOrEmpty(tlvm.CurrentChargePointId))
            {
                Logger.LogTrace("Export: Loading charge point transactions...");
                tlvm.Transactions = DbContext.Transactions
                    .Where(t => t.ChargePointId == tlvm.CurrentChargePointId &&
                                t.ConnectorId == tlvm.CurrentConnectorId &&
                                t.StartTime >= DateTime.UtcNow.AddDays(-1 * days))
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
                foreach (var t in tlvm.Transactions)
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

                    worksheet.Cell(row, 1).Value = tlvm.CurrentConnectorName;
                    worksheet.Cell(row, 2).Value = t.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cell(row, 3).Value = startTag;
                    worksheet.Cell(row, 4).Value = $"{t.MeterStart:0.0##}";
                    worksheet.Cell(row, 5).Value = t.StopTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
                    worksheet.Cell(row, 6).Value = stopTag;
                    worksheet.Cell(row, 7).Value = t.MeterStop.HasValue ? $"{t.MeterStop:0.0##}" : string.Empty;
                    worksheet.Cell(row, 8).Value = t.MeterStop.HasValue ? $"{t.MeterStop - t.MeterStart:0.0##}" : string.Empty;

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
