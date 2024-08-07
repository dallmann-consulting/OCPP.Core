using System.Collections.Generic;
using System;

namespace OCPP.Core.Management.Models
{
    public class ChargeReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime StopDate { get; set; }
        public List<GroupReport> Groups { get; set; }
    }

    public class GroupReport
    {
        public string GroupName { get; set; }
        public List<TagReport> Tags { get; set; }
    }

    public class TagReport
    {
        public string TagName { get; set; }
        public List<TransactionReport> Transactions { get; set; }
    }

    public class TransactionReport
    {
        public int TransactionId { get; set; }
        public string ChargePointId { get; set; }
        public int ConnectorId { get; set; }
        public string StartTagId { get; set; }
        public DateTime StartTime { get; set; }
        public double MeterStart { get; set; }
        public string StartResult { get; set; }
        public string StopTagId { get; set; }
        public DateTime? StopTime { get; set; }
        public double? MeterStop { get; set; }
        public string StopReason { get; set; }
        public double? Energy => MeterStop.HasValue ? MeterStop.Value - MeterStart : (double?)null;
    }
}
