using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Models
{
    public class TransactionListViewModel
    {
        public List<ChargePoint> ChargePoints { get; set; }

        public Dictionary<string, ChargeTag> ChargeTags { get; set; }

        public string CurrentChargePointId { get; set; }

        public string CurrentChargePointName { get; set; }

        public List<Transaction> Transactions { get; set; }

        public int Timespan { get; set; }

    }
}
