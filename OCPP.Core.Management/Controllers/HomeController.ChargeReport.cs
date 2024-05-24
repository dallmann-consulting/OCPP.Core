using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OCPP.Core.Management.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.InkML;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargeReport(DateTime? startTime, DateTime? stopTime)
        {
            try
            {
                var report = GenerateReport(startTime, stopTime);
                return View(report);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error loading charge points from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult ChargeReportCsv(DateTime? startTime, DateTime? stopTime)
        {
            try
            {
                var report = GenerateReport(startTime, stopTime);
                var csv = new StringBuilder();

                csv.AppendLine("Group Name,Tag Name,Energy (kWh)");
                foreach (var group in report.Groups)
                {
                    foreach (var tag in group.Tags)
                    {
                        var totalEnergy = tag.Transactions
                            .Where(t => t.Energy.HasValue)
                            .Sum(t => t.Energy.Value);
                        csv.AppendLine($"{group.GroupName},{tag.TagName},{Math.Round(totalEnergy, 2)}");
                    }
                }

                var fileName = $"ChargeReport_{DateTime.Now:yyyyMMddHHmmss}.csv";
                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating CSV report");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult ChargeReportXlsx(DateTime? startTime, DateTime? stopTime)
        {
            try
            {
                var report = GenerateReport(startTime, stopTime);
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Charge Report");

                worksheet.Cell(1, 1).Value = "Group Name";
                worksheet.Cell(1, 2).Value = "Tag Name";
                worksheet.Cell(1, 3).Value = "Energy (kWh)";

                var row = 2;
                foreach (var group in report.Groups)
                {
                    foreach (var tag in group.Tags)
                    {
                        var totalEnergy = tag.Transactions
                            .Where(t => t.Energy.HasValue)
                            .Sum(t => t.Energy.Value);

                        worksheet.Cell(row, 1).Value = group.GroupName;
                        worksheet.Cell(row, 2).Value = tag.TagName;
                        worksheet.Cell(row, 3).Value = Math.Round(totalEnergy, 2);
                        row++;
                    }
                }

                worksheet.Columns().AdjustToContents(); // Auto-scaling the column width

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                var fileName = $"ChargeReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating XLSX report");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult AllTransactionsCsv(DateTime? startTime, DateTime? stopTime)
        {
            try
            {
                var transactions = GetAllTransactions(startTime, stopTime);
                var csv = new StringBuilder();

                csv.AppendLine("Transaction ID,Charge Point ID,Connector ID,Start Tag ID,Start Tag Name,Start Time,Meter Start,Start Result,Stop Tag ID,Stop Tag Name,Stop Time,Meter Stop,Stop Reason,Energy (kWh)");
                foreach (var transaction in transactions)
                {
                    var energy = transaction.MeterStop.HasValue ? (transaction.MeterStop.Value - transaction.MeterStart) : (double?)null;
                    csv.AppendLine($"{transaction.TransactionId},{transaction.ChargePointId},{transaction.ConnectorId},{transaction.StartTag.TagId},{transaction.StartTag.TagName},{transaction.StartTime},{transaction.MeterStart},{transaction.StartResult},{transaction.StopTag?.TagId},{transaction.StopTag?.TagName},{transaction.StopTime},{transaction.MeterStop},{transaction.StopReason},{energy}");
                }

                var fileName = $"AllTransactions_{DateTime.Now:yyyyMMddHHmmss}.csv";
                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating CSV for all transactions");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult AllTransactionsXlsx(DateTime? startTime, DateTime? stopTime)
        {
            try
            {
                var transactions = GetAllTransactions(startTime, stopTime);
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("All Transactions");

                worksheet.Cell(1, 1).Value = "Transaction ID";
                worksheet.Cell(1, 2).Value = "Charge Point ID";
                worksheet.Cell(1, 3).Value = "Connector ID";
                worksheet.Cell(1, 4).Value = "Start Tag ID";
                worksheet.Cell(1, 5).Value = "Start Tag Name";
                worksheet.Cell(1, 6).Value = "Start Time";
                worksheet.Cell(1, 7).Value = "Meter Start";
                worksheet.Cell(1, 8).Value = "Start Result";
                worksheet.Cell(1, 9).Value = "Stop Tag ID";
                worksheet.Cell(1, 10).Value = "Stop Tag Name";
                worksheet.Cell(1, 11).Value = "Stop Time";
                worksheet.Cell(1, 12).Value = "Meter Stop";
                worksheet.Cell(1, 13).Value = "Stop Reason";
                worksheet.Cell(1, 14).Value = "Energy (kWh)";

                var row = 2;
                foreach (var transaction in transactions)
                {
                    var energy = transaction.MeterStop.HasValue ? (transaction.MeterStop.Value - transaction.MeterStart) : (double?)null;
                    worksheet.Cell(row, 1).Value = transaction.TransactionId;
                    worksheet.Cell(row, 2).Value = transaction.ChargePointId;
                    worksheet.Cell(row, 3).Value = transaction.ConnectorId;
                    worksheet.Cell(row, 4).Value = transaction.StartTag.TagId;
                    worksheet.Cell(row, 5).Value = transaction.StartTag.TagName;
                    worksheet.Cell(row, 6).Value = transaction.StartTime;
                    worksheet.Cell(row, 7).Value = transaction.MeterStart;
                    worksheet.Cell(row, 8).Value = transaction.StartResult;
                    worksheet.Cell(row, 9).Value = transaction.StopTag?.TagId;
                    worksheet.Cell(row, 10).Value = transaction.StopTag?.TagName;
                    worksheet.Cell(row, 11).Value = transaction.StopTime;
                    worksheet.Cell(row, 12).Value = transaction.MeterStop;
                    worksheet.Cell(row, 13).Value = transaction.StopReason;
                    worksheet.Cell(row, 14).Value = energy;
                    row++;
                }

                worksheet.Columns().AdjustToContents(); // Auto-scaling the column width

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                var fileName = $"AllTransactions_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating XLSX for all transactions");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }


        private ChargeReportViewModel GenerateReport(DateTime? startTime, DateTime? stopTime)
        {
            startTime ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1); // Default to first day of previous month
            stopTime = stopTime.HasValue ? stopTime.Value.Date.AddDays(1).AddSeconds(-1) : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddSeconds(-1); // Default to last second of previous month

            var transactions = DbContext.Set<Transaction>()
                .Include(t => t.StartTag) // Ensure StartTag is included in the query
                .Where(t => t.StartTime >= startTime && t.StartTime <= stopTime && (!t.StopTime.HasValue || t.StopTime <= stopTime))
                .ToList();

            var tags = DbContext.Set<ChargeTag>().ToList();

            return new ChargeReportViewModel
            {
                StartTime = startTime.Value,
                StopTime = stopTime.Value,
                Groups = transactions
                    .GroupBy(t => t.StartTag.ParentTagId)
                    .OrderBy(g => g.Key) // Order groups by name
                    .Select(g => new GroupReport
                    {
                        GroupName = g.Key,
                        Tags = g.GroupBy(t => tags.FirstOrDefault(tag => tag.TagId == t.StartTag.TagId)?.TagName)
                                .OrderBy(tg => tg.Key) // Order tags by name
                                .Select(tg => new TagReport
                                {
                                    TagName = tg.Key,
                                    Transactions = tg.Select(t => new TransactionReport
                                    {
                                        TransactionId = t.TransactionId,
                                        ChargePointId = t.ChargePointId,
                                        ConnectorId = t.ConnectorId,
                                        StartTagId = t.StartTag.TagId,
                                        StartTime = t.StartTime,
                                        MeterStart = t.MeterStart,
                                        StartResult = t.StartResult,
                                        StopTagId = t.StartTag.TagId,
                                        StopTime = t.StopTime,
                                        MeterStop = t.MeterStop,
                                        StopReason = t.StopReason
                                    }).ToList()
                                }).ToList()
                    }).ToList()
            };
        }

        private List<Transaction> GetAllTransactions(DateTime? startTime, DateTime? stopTime)
        {
            startTime ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1); // Default to first day of previous month
            stopTime = stopTime.HasValue ? stopTime.Value.Date.AddDays(1).AddSeconds(-1) : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddSeconds(-1); // Default to last second of previous month

            return DbContext.Set<Transaction>()
                .Include(t => t.StartTag)
                .Include(t => t.StopTag)
                .Where(t => t.StartTime >= startTime && t.StartTime <= stopTime && (!t.StopTime.HasValue || t.StopTime <= stopTime))
                .AsNoTracking()
                .ToList();
        }
    }
}
