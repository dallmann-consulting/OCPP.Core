using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Models
{
    public class ChargePointsOverviewViewModel
    {
        public string ChargePointId { get; set; }

        public string Name { get; set; }

        public string Comment { get; set; }

        public int LastTransactionId { get; set; }

        public int ConnectorId { get; set; }

        public int MeterStart { get; set; }

        public int? MeterStop { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? StopTime { get; set; }
    }
}
