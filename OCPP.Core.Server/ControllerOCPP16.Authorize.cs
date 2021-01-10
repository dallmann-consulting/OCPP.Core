/*
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
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleAuthorize(Message msgIn, Message msgOut)
        {
            string errorCode = null;
            AuthorizeResponse authorizeResponse = new AuthorizeResponse();

            try
            {
                Logger.LogTrace("Processing authorize request...");
                AuthorizeRequest authorizeRequest = JsonConvert.DeserializeObject<AuthorizeRequest>(msgIn.JsonPayload);
                Logger.LogTrace("Authorize => Message deserialized");
                string idTag = authorizeRequest.IdTag;

                authorizeResponse.IdTagInfo.ParentIdTag = string.Empty;
                authorizeResponse.IdTagInfo.ExpiryDate = DateTimeOffset.UtcNow;
                try
                {
                    using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                    {
                        ChargeTag ct = dbContext.Find<ChargeTag>(idTag);
                        if (ct != null)
                        {
                            authorizeResponse.IdTagInfo.ExpiryDate = ct.ExpiryDate.HasValue ? ct.ExpiryDate.Value : new DateTime(2999, 12, 31);
                            authorizeResponse.IdTagInfo.ParentIdTag = ct.ParentTagId;
                            if (ct.Blocked.HasValue && ct.Blocked.Value)
                            {
                                authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Blocked;
                            }
                            else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                            {
                                authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Expired;
                            }
                            else
                            {
                                authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                            }
                        }
                        else
                        {
                            authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                        }

                        Logger.LogInformation("Authorize => Status: {0}", authorizeResponse.IdTagInfo.Status);
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "Authorize => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                    authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(authorizeResponse);
                Logger.LogTrace("Authorize => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Authorize => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, null,msgIn.Action, authorizeResponse.IdTagInfo?.Status.ToString(), errorCode);
            return errorCode;
        }
    }
}
