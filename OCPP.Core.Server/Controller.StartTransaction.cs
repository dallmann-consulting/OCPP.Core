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
        public string HandleStartTransaction(Message msgIn, Message msgOut)
        {
            string errorCode = null;
            StartTransactionResponse startTransactionResponse = new StartTransactionResponse();

            int? connectorId = null;

            try
            {
                Logger.LogTrace("Processing startTransaction request...");
                StartTransactionRequest startTransactionRequest = JsonConvert.DeserializeObject<StartTransactionRequest>(msgIn.JsonPayload);
                Logger.LogTrace("StartTransaction => Message deserialized");

                string idTag = startTransactionRequest.IdTag;
                connectorId = startTransactionRequest.ConnectorId;

                startTransactionResponse.IdTagInfo.ParentIdTag = string.Empty;
                startTransactionResponse.IdTagInfo.ExpiryDate = DateTime.Now;
                try
                {
                    using (OCPPCoreContext dbContext = new OCPPCoreContext(Configuration))
                    {
                        ChargeTag ct = dbContext.Find<ChargeTag>(idTag);
                        if (ct != null)
                        {
                            startTransactionResponse.IdTagInfo.ExpiryDate = ct.ExpiryDate.HasValue ? ct.ExpiryDate.Value : new DateTime(2999, 12, 31);
                            startTransactionResponse.IdTagInfo.ParentIdTag = ct.ParentTagId;
                            if (ct.Blocked.HasValue && ct.Blocked.Value)
                            {
                                startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Blocked;
                            }
                            else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                            {
                                startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Expired;
                            }
                            else
                            {
                                startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                            }
                        }
                        else
                        {
                            startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                        }

                        Logger.LogInformation("StartTransaction => Status: {0}", startTransactionResponse.IdTagInfo.Status);

                        try
                        {
                            Transaction transaction = new Transaction();
                            transaction.ChargePointId = CurrentChargePoint?.ChargePointId;
                            transaction.ConnectorId = startTransactionRequest.ConnectorId;
                            transaction.StartTagId = idTag;
                            transaction.StartTime = startTransactionRequest.Timestamp;
                            transaction.MeterStart = startTransactionRequest.MeterStart;
                            transaction.StartResult = startTransactionResponse.IdTagInfo.Status.ToString();
                            dbContext.Add<Transaction>(transaction);

                            dbContext.SaveChanges();
                            startTransactionResponse.TransactionId = transaction.TransactionId;
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "StartTransaction => Exception writing transaction: chargepoint={0} / tag={1}", CurrentChargePoint?.ChargePointId, idTag);
                            errorCode = ErrorCodes.InternalError;
                        }
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "StartTransaction => Exception reading charge tag ({0}): {1}", idTag, exp.Message);
                    startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(startTransactionResponse);
                Logger.LogTrace("StartTransaction => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "StartTransaction => Exception: {0}", exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(CurrentChargePoint?.ChargePointId, connectorId, msgIn.Action, startTransactionResponse.IdTagInfo?.Status.ToString(), errorCode);
            return errorCode;
        }
    }
}
