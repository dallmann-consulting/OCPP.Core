/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
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

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP20;
using System;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP20
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
                idTag = CleanChargeTagId(authorizeRequest.IdToken?.IdToken, Logger);

                authorizeResponse.IdTokenInfo = InternalAuthorize(idTag, ocppMiddleware);

                authorizeResponse.CustomData = new CustomDataType();
                authorizeResponse.CustomData.VendorId = VendorId;

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

        /// <summary>
        /// Authorization logic for reuseability
        /// </summary>
        internal IdTokenInfoType InternalAuthorize(string idTag, OCPPMiddleware ocppMiddleware)
        {
            IdTokenInfoType idTagInfo = new IdTokenInfoType();
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
                    idTagInfo.Status = AuthorizationStatusEnumType.Accepted;
                }
                else
                {
                    idTagInfo.Status = AuthorizationStatusEnumType.Invalid;
                }
                Logger.LogInformation("Authorize => Extension auth. : Charge tag='{0}' => Status: {1}", idTag, idTagInfo.Status);
            }
            else
            {
                try
                {
                    ChargeTag ct = DbContext.Find<ChargeTag>(idTag);
                    if (ct != null)
                    {
                        if (!string.IsNullOrEmpty(ct.ParentTagId))
                        {
                            idTagInfo.GroupIdToken = new IdTokenType();
                            idTagInfo.GroupIdToken.IdToken = ct.ParentTagId;
                        }

                        if (ct.Blocked.HasValue && ct.Blocked.Value)
                        {
                            idTagInfo.Status = AuthorizationStatusEnumType.Blocked;
                        }
                        else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                        {
                            idTagInfo.Status = AuthorizationStatusEnumType.Expired;
                        }
                        else
                        {
                            idTagInfo.Status = AuthorizationStatusEnumType.Accepted;
                        }
                    }
                    else
                    {
                        idTagInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }

                    Logger.LogInformation("Authorize => Status: {0}", idTagInfo.Status);
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "Authorize => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                    idTagInfo.Status = AuthorizationStatusEnumType.Invalid;
                }
            }

            return idTagInfo;
        }
    }
}
