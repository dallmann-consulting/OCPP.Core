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
using OCPP.Core.Server.Messages_OCPP20;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
    {
        public string HandleAuthorize(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            string errorCode = null;
            AuthorizeResponse authorizeResponse = new AuthorizeResponse();

            string idTag = null;
            try
            {
                Logger.LogTrace("Processing authorize request...");
                AuthorizeRequest authorizeRequest = DeserializeMessage<AuthorizeRequest>(msgIn);
                Logger.LogTrace("Authorize => Message deserialized");
                idTag = CleanChargeTagId(authorizeRequest.IdToken?.IdToken, Logger);

                authorizeResponse.CustomData = new CustomDataType();
                authorizeResponse.CustomData.VendorId = VendorId;

                authorizeResponse.IdTokenInfo = new IdTokenInfoType();
                authorizeResponse.IdTokenInfo.CustomData = new CustomDataType();
                authorizeResponse.IdTokenInfo.CustomData.VendorId = VendorId;
                authorizeResponse.IdTokenInfo.GroupIdToken = new IdTokenType();
                authorizeResponse.IdTokenInfo.GroupIdToken.CustomData = new CustomDataType();
                authorizeResponse.IdTokenInfo.GroupIdToken.CustomData.VendorId = VendorId;
                authorizeResponse.IdTokenInfo.GroupIdToken.IdToken = string.Empty;

                try
                {
                    ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                    if (ct != null)
                    {
                        if (!string.IsNullOrEmpty(ct.ParentTagId))
                        {
                            authorizeResponse.IdTokenInfo.GroupIdToken.IdToken = ct.ParentTagId;
                        }

                        if (ct.Blocked.HasValue && ct.Blocked.Value)
                        {
                            authorizeResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                        }
                        else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                        {
                            authorizeResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                        }
                        else
                        {
                            authorizeResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                        }
                    }
                    else
                    {
                        authorizeResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }

                    Logger.LogInformation("Authorize => Status: {0}", authorizeResponse.IdTokenInfo.Status);
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "Authorize => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                    authorizeResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(authorizeResponse);
                Logger.LogTrace("Authorize => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Authorize => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, $"'{idTag}'=>{authorizeResponse.IdTokenInfo?.Status}", errorCode);
            return errorCode;
        }
    }
}
