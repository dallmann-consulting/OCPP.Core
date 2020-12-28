using System;
using System.Collections.Generic;

#nullable disable

namespace OCPP.Core.Database
{
    public partial class Transaction
    {
        public int TransactionId { get; set; }
        public string ChargePointId { get; set; }
        public int ConnectorId { get; set; }
        public string StartTagId { get; set; }
        public DateTime StartTime { get; set; }
        public int MeterStart { get; set; }
        public string StartResult { get; set; }
        public string StopTagId { get; set; }
        public DateTime? StopTime { get; set; }
        public int? MeterStop { get; set; }
        public string StopReason { get; set; }

        public virtual ChargePoint ChargePoint { get; set; }
    }
}
