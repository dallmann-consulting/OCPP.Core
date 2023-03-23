using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Server.Models
{
    public class TagUpdateModel
    {
        [JsonProperty("tagId", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public string TagId { get; set; }

        [JsonProperty("expiryDate", Required = Required.Default)]
        public DateTimeOffset ExpiryDate { get; set; } = DateTimeOffset.MaxValue;
    }
}
