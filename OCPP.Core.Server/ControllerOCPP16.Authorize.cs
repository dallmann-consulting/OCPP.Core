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

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP16;
using System;
using System.Linq;

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

                authorizeResponse.IdTagInfo = InternalAuthorize(idTag, ocppMiddleware, 0, AuthAction.Authorize, string.Empty, string.Empty, false);

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

        /// <summary>
        /// Authorization logic for reuseability
        /// </summary>
        internal IdTagInfo InternalAuthorize(string idTag, OCPPMiddleware ocppMiddleware, int connectorId, AuthAction authAction, string transactionUid, string transactionStartId, bool denyConcurrentTx)
        {
            IdTagInfo idTagInfo = new IdTagInfo();
            idTagInfo.ParentIdTag = string.Empty;
            idTagInfo.ExpiryDate = MaxExpiryDate;

            bool? externalAuthResult = null;
            try
            {
                externalAuthResult = ocppMiddleware.ProcessExternalAuthorizations(authAction, idTag, ChargePointStatus.Id, connectorId, transactionUid, transactionStartId);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "InternalAuthorize => Exception from external authorization (Action={0}, Tag={1}): {2}", authAction, idTag, exp.Message);
            }

            if (externalAuthResult.HasValue)
            {
                if (externalAuthResult.Value)
                {
                    idTagInfo.Status = IdTagInfoStatus.Accepted;
                }
                else
                {
                    idTagInfo.Status = IdTagInfoStatus.Invalid;
                }
                Logger.LogInformation("InternalAuthorize => Extension auth. : Action={0}, Tag='{1}' => Status: {2}", authAction, idTag, idTagInfo.Status);
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
                            idTagInfo.ExpiryDate = ct.ExpiryDate.Value;
                        }
                        idTagInfo.ParentIdTag = ct.ParentTagId;
                        if (ct.Blocked.HasValue && ct.Blocked.Value)
                        {
                            idTagInfo.Status = IdTagInfoStatus.Blocked;
                        }
                        else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                        {
                            idTagInfo.Status = IdTagInfoStatus.Expired;
                        }
                        else
                        {
                            idTagInfo.Status = IdTagInfoStatus.Accepted;

                            if (denyConcurrentTx)
                            {
                                // Check that no open transaction with this idTag exists
                                Transaction tx = DbContext.Transactions
                                    .Where(t => !t.StopTime.HasValue && t.StartTagId == ct.TagId)
                                    .OrderByDescending(t => t.TransactionId)
                                    .FirstOrDefault();

                                if (tx != null)
                                {
                                    idTagInfo.Status = IdTagInfoStatus.ConcurrentTx;
                                }
                            }
                        }
                    }
                    else
                    {
                        idTagInfo.Status = IdTagInfoStatus.Invalid;
                    }
                    Logger.LogInformation("InternalAuthorize => DB-Auth : Action={0}, Tag='{1}' => Status: {2}", authAction, idTag, idTagInfo.Status);
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "InternalAuthorize => Exception reading charge tag (action={0}, tag={1}): {2}", authAction, idTag, exp.Message);
                    idTagInfo.Status = IdTagInfoStatus.Invalid;
                }
            }

            return idTagInfo;
        }
    }
}
