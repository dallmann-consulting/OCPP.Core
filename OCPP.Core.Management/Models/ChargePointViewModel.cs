using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Models
{
    public class ChargePointViewModel
    {
        public List<ChargePoint> ChargePoints { get; set; }

        public string CurrentId { get; set; }


        [Required, StringLength(100)]
        public string ChargePointId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string Comment { get; set; }
    }
}
