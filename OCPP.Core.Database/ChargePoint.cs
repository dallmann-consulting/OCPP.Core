using System;
using System.Collections.Generic;

#nullable disable

namespace OCPP.Core.Database
{
    public partial class ChargePoint
    {
        public ChargePoint()
        {
            Transactions = new HashSet<Transaction>();
        }

        public string ChargePointId { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }

        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}
