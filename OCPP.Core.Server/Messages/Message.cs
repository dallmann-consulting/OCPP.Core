using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server.Messages
{
    /// <summary>
    /// Warpper object for OCPP Message
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Message type
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// Message ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// Action
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// JSON-Payload
        /// </summary>
        public string JsonPayload { get; set; }

        /// <summary>
        /// Error-Code
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Error-Description
        /// </summary>
        public string ErrorDescription { get; set; }

        /// <summary>
        /// Empty constructor
        /// </summary>
        public Message()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public Message(string messageType, string uniqueId, string action, string jsonPayload)
        {
            MessageType = messageType;
            UniqueId = uniqueId;
            Action = action;
            JsonPayload = jsonPayload;
        }
    }
}
