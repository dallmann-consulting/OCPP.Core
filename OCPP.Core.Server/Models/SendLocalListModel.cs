using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Server.Models
{
    public class SendLocalListModel
    {
        [JsonProperty("listVersion", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public int ListVersion { get; set; }

        [JsonProperty("tags", Required = Required.Always)]
        [Required(AllowEmptyStrings = false)]
        public List<TagUpdateModel> Tags { get; set; }
    }
}
