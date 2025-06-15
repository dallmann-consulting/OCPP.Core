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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleAuthorize(OCPPMessage msgIn, OCPPMessage msgOut, OCPPMiddleware ocppMiddleware)
        {
            string errorCode = null;
            AuthorizeResponse authorizeResponse = new AuthorizeResponse();

            string idTag = null;
            try
            {
                Logger.LogTrace("Processing authorize request...");
                AuthorizeRequest authorizeRequest = DeserializeMessage<AuthorizeRequest>(msgIn);
                Logger.LogTrace("Authorize => Message deserialized");
                idTag = CleanChargeTagId(authorizeRequest.IdTag, Logger);

                authorizeResponse.IdTagInfo.ParentIdTag = string.Empty;
                authorizeResponse.IdTagInfo.ExpiryDate = DateTimeOffset.UtcNow.AddMinutes(5);   // default: 5 minutes

                bool? externalAuthResult = null;
                try
                {
                    externalAuthResult = ocppMiddleware.ProcessExternalAuthorizations(AuthAction.Authorize, idTag, ChargePointStatus.Id, 0, string.Empty, string.Empty);
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "Authorize => Exception from external authorization: {0}", exp.Message);
                }

                if (externalAuthResult.HasValue)
                {
                    if (externalAuthResult.Value)
                    {
                        authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                    }
                    else
                    {
                        authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                    }
                    Logger.LogInformation("Authorize => Extension auth. : Charge tag='{0}' => Status: {1}", idTag, authorizeResponse.IdTagInfo.Status);
                }
                else
                {
                    try
                    {
                        ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                        if (ct != null)
                        {
                            if (ct.ExpiryDate.HasValue)
                            {
                                authorizeResponse.IdTagInfo.ExpiryDate = ct.ExpiryDate.Value;
                            }
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
                        Logger.LogInformation("Authorize => Inernal auth. : Charge tag='{0}' => Status: {1}", idTag, authorizeResponse.IdTagInfo.Status);
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "Authorize => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                        authorizeResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                    }
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(authorizeResponse);
                Logger.LogTrace("Authorize => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Authorize => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, null,msgIn.Action, $"'{idTag}'=>{authorizeResponse.IdTagInfo?.Status}", errorCode);
            return errorCode;
        }
    }
}
