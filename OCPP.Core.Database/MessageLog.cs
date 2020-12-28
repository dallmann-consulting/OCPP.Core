using System;
using System.Collections.Generic;

#nullable disable

namespace OCPP.Core.Database
{
    public partial class MessageLog
    {
        public int LogId { get; set; }
        public DateTime LogTime { get; set; }
        public string ChargePointId { get; set; }
        public int? ConnectorId { get; set; }
        public string Message { get; set; }
        public string Result { get; set; }
        public string ErrorCode { get; set; }
    }
}
