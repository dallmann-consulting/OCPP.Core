﻿/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages;

namespace OCPP.Core.Server
{
    public partial class Controller
    {
        public string HandleDataTransfer(Message msgIn, Message msgOut)
        {
            string errorCode = null;
            DataTransferResponse dataTransferResponse = new DataTransferResponse();

            bool msgWritten = false;

            try
            {
                Logger.LogTrace("Processing data transfer...");
                DataTransferRequest dataTransferRequest = JsonConvert.DeserializeObject<DataTransferRequest>(msgIn.JsonPayload);
                Logger.LogTrace("DataTransfer => Message deserialized");

                if (CurrentChargePoint != null)
                {
                    // Known charge station
                    msgWritten = WriteMessageLog(CurrentChargePoint.ChargePointId, null, msgIn.Action, string.Format("VendorId={0} / MessageId={1} / Data={2}", dataTransferRequest.VendorId, dataTransferRequest.MessageId, dataTransferRequest.Data), errorCode);
                    dataTransferResponse.Status = DataTransferResponseStatus. UnknownVendorId;
                }
                else
                {
                    // Unknown charge station
                    errorCode = ErrorCodes.GenericError;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(dataTransferResponse);
                Logger.LogTrace("DataTransfer => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "DataTransfer => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.InternalError;
            }

            if (!msgWritten)
            {
                WriteMessageLog(CurrentChargePoint.ChargePointId, null, msgIn.Action, null, errorCode);
            }
            return errorCode;
        }
    }
}
