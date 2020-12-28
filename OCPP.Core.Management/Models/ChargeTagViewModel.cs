using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Models
{
    public class ChargeTagViewModel
    {
        public List<ChargeTag> ChargeTags { get; set; }

        public string CurrentTagId { get; set; }


        [Required, StringLength(50)]
        public string TagId { get; set; }

        [Required, StringLength(200)]
        public string TagName { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ExpiryDate { get; set; }

        public bool Blocked { get; set; }
    }
}
