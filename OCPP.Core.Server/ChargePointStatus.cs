using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public class ChargePointStatus
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public ChargePointStatus(ChargePoint chargePoint)
        {
            Id = chargePoint.ChargePointId;
            Name = chargePoint.Name;
        }
    }
}
