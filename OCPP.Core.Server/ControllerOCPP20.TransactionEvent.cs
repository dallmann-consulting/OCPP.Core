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
        public string HandleTransactionEvent(Message msgIn, Message msgOut)
        {
            string errorCode = null;
            TransactionEventResponse transactionEventResponse = new TransactionEventResponse();
            transactionEventResponse.CustomData = new CustomDataType();
            transactionEventResponse.CustomData.VendorId = VendorId;
            transactionEventResponse.IdTokenInfo = new IdTokenInfoType();

            int connectorId = 0;

            try
            {
                Logger.LogTrace("TransactionEvent => Processing transactionEvent request...");
                TransactionEventRequest transactionEventRequest = JsonConvert.DeserializeObject<TransactionEventRequest>(msgIn.JsonPayload);
                Logger.LogTrace("TransactionEvent => Message deserialized");

                string idTag = transactionEventRequest.IdToken.IdToken;
                connectorId = (transactionEventRequest.Evse != null) ? transactionEventRequest.Evse.ConnectorId : 0;

                if (transactionEventRequest.EventType == TransactionEventEnumType.Started)
                {
                    try
                    {
                        #region Start Transaction
                        using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                        {
                            ChargeTag ct = dbContext.Find<ChargeTag>(idTag);
                            if (ct != null)
                            {
                                if (ct.Blocked.HasValue && ct.Blocked.Value)
                                {
                                    Logger.LogInformation("StartTransaction => Tag '{1}' blocked)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                                }
                                else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                                {
                                    Logger.LogInformation("StartTransaction => Tag '{1}' expired)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                                }
                                else
                                {
                                    Logger.LogInformation("StartTransaction => Tag '{1}' accepted)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                }
                            }
                            else
                            {
                                Logger.LogInformation("StartTransaction => Tag '{1}' unknown)", idTag);
                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Unknown;
                            }

                            try
                            {
                                double meterValue = GetMeterValue(transactionEventRequest);
                                Logger.LogInformation("StartTransaction => MeterValue='{0}'", meterValue);

                                Transaction transaction = new Transaction();
                                transaction.Uid = transactionEventRequest.TransactionInfo.TransactionId;
                                transaction.ChargePointId = ChargePointStatus?.Id;
                                transaction.ConnectorId = connectorId;
                                transaction.StartTagId = idTag;
                                transaction.StartTime = transactionEventRequest.Timestamp;
                                transaction.MeterStart = meterValue;
                                transaction.StartResult = transactionEventRequest.TriggerReason.ToString();
                                dbContext.Add<Transaction>(transaction);

                                dbContext.SaveChanges();
                                //transactionEventResponse.TransactionId= transaction.TransactionId.ToString();
                            }
                            catch (Exception exp)
                            {
                                Logger.LogError(exp, "StartTransaction => Exception writing transaction: chargepoint={0} / tag={1}", ChargePointStatus?.Id, idTag);
                                errorCode = ErrorCodes.InternalError;
                            }
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "StartTransaction => Exception: {0}", exp.Message);
                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Updated)
                {
                    try
                    {
                        #region Update Transaction
                        using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                        {
                            Transaction transaction = dbContext.Transactions
                                .Where(t => t.Uid == transactionEventRequest.TransactionInfo.TransactionId)
                                .OrderByDescending(t => t.TransactionId)
                                .FirstOrDefault();
                            if (transaction == null ||
                                transaction.ChargePointId != ChargePointStatus.Id ||
                                transaction.StopTime.HasValue)
                            {
                                // unknown transaction id or already stopped transaction
                                // => find latest transaction for the charge point and check if its open
                                Logger.LogWarning("UpdateTransaction => Unknown or closed transaction uid={0}", transactionEventRequest.TransactionInfo?.TransactionId);
                                // find latest transaction for this charge point
                                transaction = dbContext.Transactions
                                    .Where(t => t.ChargePointId == ChargePointStatus.Id)
                                    .OrderByDescending(t => t.TransactionId)
                                    .FirstOrDefault();

                                if (transaction != null)
                                {
                                    Logger.LogTrace("UpdateTransaction => Last transaction id={0} / Start='{1}' / Stop='{2}'", transaction.TransactionId, transaction.StartTime.ToString(SimpleTimeStampFormat), transaction?.StopTime?.ToString(SimpleTimeStampFormat));
                                    if (transaction.StopTime.HasValue)
                                    {
                                        Logger.LogTrace("UpdateTransaction => Last transaction (id={0}) is already closed ", transaction.TransactionId);
                                        transaction = null;
                                    }
                                }
                                else
                                {
                                    Logger.LogTrace("UpdateTransaction => Found no transaction for charge point '{0}'", ChargePointStatus.Id);
                                }
                            }

                            if (transaction != null)
                            {
                                // write current meter value in "stop" value
                                double meterValue = GetMeterValue(transactionEventRequest);
                                Logger.LogInformation("UpdateTransaction => MeterValue='{0}'", meterValue);
                                if (meterValue >= 0)
                                {
                                    transaction.MeterStop = meterValue;
                                    dbContext.SaveChanges();
                                }
                            }
                            else
                            {
                                Logger.LogError("UpdateTransaction => Unknown transaction: uid='{0}' / chargepoint='{1}' / tag={2}", transactionEventRequest.TransactionInfo?.TransactionId, ChargePointStatus?.Id, idTag);
                                WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, string.Format("UnknownTransaction:UID={0}/Meter={1}", transactionEventRequest.TransactionInfo?.TransactionId, GetMeterValue(transactionEventRequest)), errorCode);
                                errorCode = ErrorCodes.PropertyConstraintViolation;
                            }
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "UpdateTransaction => Exception: {0}", exp.Message);
                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }
                }
                else if (transactionEventRequest.EventType == TransactionEventEnumType.Ended)
                {
                    try
                    {
                        #region End Transaction
                        using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                        {
                            ChargeTag ct = dbContext.Find<ChargeTag>(idTag);
                            if (ct != null)
                            {
                                if (ct.Blocked.HasValue && ct.Blocked.Value)
                                {
                                    Logger.LogInformation("EndTransaction => Tag '{1}' blocked)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Blocked;
                                }
                                else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                                {
                                    Logger.LogInformation("EndTransaction => Tag '{1}' expired)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Expired;
                                }
                                else
                                {
                                    Logger.LogInformation("EndTransaction => Tag '{1}' accepted)", idTag);
                                    transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Accepted;
                                }
                            }
                            else
                            {
                                Logger.LogInformation("EndTransaction => Tag '{1}' unknown)", idTag);
                                transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Unknown;
                            }

                            Transaction transaction = dbContext.Transactions
                                .Where(t => t.Uid == transactionEventRequest.TransactionInfo.TransactionId)
                                .OrderByDescending(t => t.TransactionId)
                                .FirstOrDefault();
                            if (transaction == null ||
                                transaction.ChargePointId != ChargePointStatus.Id ||
                                transaction.StopTime.HasValue)
                            {
                                // unknown transaction id or already stopped transaction
                                // => find latest transaction for the charge point and check if its open
                                Logger.LogWarning("EndTransaction => Unknown or closed transaction uid={0}", transactionEventRequest.TransactionInfo?.TransactionId);
                                // find latest transaction for this charge point
                                transaction = dbContext.Transactions
                                    .Where(t => t.ChargePointId == ChargePointStatus.Id)
                                    .OrderByDescending(t => t.TransactionId)
                                    .FirstOrDefault();

                                if (transaction != null)
                                {
                                    Logger.LogTrace("EndTransaction => Last transaction id={0} / Start='{1}' / Stop='{2}'", transaction.TransactionId, transaction.StartTime.ToString(SimpleTimeStampFormat), transaction?.StopTime?.ToString(SimpleTimeStampFormat));
                                    if (transaction.StopTime.HasValue)
                                    {
                                        Logger.LogTrace("EndTransaction => Last transaction (id={0}) is already closed ", transaction.TransactionId);
                                        transaction = null;
                                    }
                                }
                                else
                                {
                                    Logger.LogTrace("EndTransaction => Found no transaction for charge point '{0}'", ChargePointStatus.Id);
                                }
                            }

                            if (transaction != null)
                            {
                                // check current tag against start tag
                                bool valid = true;
                                if (!string.Equals(transaction.StartTagId, idTag, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // tags are different => same group?
                                    ChargeTag startTag = dbContext.Find<ChargeTag>(transaction.StartTagId);
                                    if (startTag != null)
                                    {
                                        if (!string.Equals(startTag.ParentTagId, ct?.ParentTagId, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            Logger.LogInformation("EndTransaction => Start-Tag ('{0}') and End-Tag ('{1}') do not match: Invalid!", transaction.StartTagId, ct?.TagId);
                                            transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                                            valid = false;
                                        }
                                        else
                                        {
                                            Logger.LogInformation("EndTransaction => Different RFID-Tags but matching group ('{0}')", ct?.ParentTagId);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogError("EndTransaction => Start-Tag not found: '{0}'", transaction.StartTagId);
                                        // assume "valid" and allow to end the transaction
                                    }
                                }

                                if (valid)
                                {
                                    // write current meter value in "stop" value
                                    double meterValue = GetMeterValue(transactionEventRequest);
                                    Logger.LogInformation("EndTransaction => MeterValue='{0}'", meterValue);

                                    transaction.StopTime = transactionEventRequest.Timestamp;
                                    transaction.MeterStop = meterValue;
                                    transaction.StopTagId = idTag;
                                    transaction.StopReason = transactionEventRequest.TriggerReason.ToString();
                                    dbContext.SaveChanges();
                                }
                            }
                            else
                            {
                                Logger.LogError("EndTransaction => Unknown transaction: uid='{0}' / chargepoint='{1}' / tag={2}", transactionEventRequest.TransactionInfo?.TransactionId, ChargePointStatus?.Id, idTag);
                                WriteMessageLog(ChargePointStatus?.Id, null, msgIn.Action, string.Format("UnknownTransaction:UID={0}/Meter={1}", transactionEventRequest.TransactionInfo?.TransactionId, GetMeterValue(transactionEventRequest)), errorCode);
                                errorCode = ErrorCodes.PropertyConstraintViolation;
                            }
                        }
                        #endregion
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "EndTransaction => Exception: {0}", exp.Message);
                        transactionEventResponse.IdTokenInfo.Status = AuthorizationStatusEnumType.Invalid;
                    }
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(transactionEventResponse);
                Logger.LogTrace("TransactionEvent => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "TransactionEvent => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, transactionEventResponse.IdTokenInfo.Status.ToString(), errorCode);
            return errorCode;
        }

        private double GetMeterValue(TransactionEventRequest transactionEventRequest)
        {
            double meterValue = -1;

            if (transactionEventRequest.MeterValue != null)
            {
                foreach (MeterValueType meterVal in transactionEventRequest.MeterValue)
                {
                    if (meterVal.SampledValue != null)
                    {
                        foreach (SampledValueType sampledVal in meterVal.SampledValue)
                        {
                            meterValue = sampledVal.Value;
                            break;
                        }
                    }
                }
            }

            Logger.LogTrace("TransactionEvent => GetMeterValue()='{0}'", meterValue);
            return meterValue;
        }
    }
}
