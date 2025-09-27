using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OCPP.Core.Test
{
    class OCPP20Test
    {
        /*
         * Configure valid data for database and installation here
         */
        private static string _chargePointId = "Test1234";
        private static string _serverUrl = $"ws://localhost:8081";
        private static string _chargeTagId = "12345";
        private static string _apiKey = "36029A5F-B736-4DA9-AE46-D66847C9062C";

        private static ClientWebSocket _webSocket = new ClientWebSocket();
        private static TaskCompletionSource<JArray>? _responseTcs;
        private static int _seqNo = 1;

        internal async static void Execute()
        {
            Console.WriteLine("Press enter to start test with OCPP 2.0.1");
            Console.ReadLine();

            try
            {

                /* 1.  Try to connect with an unknown chargepoint id => it should fail  */
                try
                {
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.AddSubProtocol("ocpp2.0.1"); // OCPP 2.0.1J

                    Console.WriteLine($"Connecting with unknown chargepoint: {_serverUrl + "/OCPP/unknown" + _chargePointId}");
                    _webSocket.ConnectAsync(new Uri(_serverUrl + "/OCPP/unknown" + _chargePointId), CancellationToken.None).Wait();
                    Console.WriteLine("BAD: Connected successful with unknown chargepoint");
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Good: Connection failed with unknown chargepoint");
                }
                finally
                {
                    if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                    {
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Simulation end", CancellationToken.None).Wait();
                    }
                }


                /* 2.  Try to connect with a known chargepoint id => it should succeed  */
                _webSocket = new ClientWebSocket();
                _webSocket.Options.AddSubProtocol("ocpp2.0.1"); // OCPP 2.0.1J

                Console.WriteLine($"Connecting with known chargepoint: {_serverUrl + "/OCPP/" + _chargePointId}");
                _webSocket.ConnectAsync(new Uri(_serverUrl + "/OCPP/" + _chargePointId), CancellationToken.None).Wait();
                Console.WriteLine("Connected!");

                // Start background task for receiving messages
                var receiveTask = Task.Run(() => ReceiveMessages());



                /* 3.  Test general messages  */
                SendAndVerifyBootNotification().Wait();

                SendAndVerifyStatusNotification(1, "Available").Wait();

                SendAndVerifyHeartbeat().Wait();

                SendAndVerifyDataTransfer().Wait();

                SendAndVerifyLogStatusNotification().Wait();

                SendAndVerifyMeterValues(1).Wait();


                /* 4.  Test authorization with unknown tag => it should fail */
                if (SendAndVerifyAuthorize("fail_" + _chargeTagId).Result)
                {
                    Console.WriteLine("BAD: Authorize with unknown charge tag succeded");
                }
                else
                {
                    Console.WriteLine("GOOD: Authorize with unknown charge tag rejected");
                }



                /* 5.  Check chargepoint status in/from server  */
                ReadServerStatus();


                /* 6.  Test authorization with known tag => it should succeed */
                if (SendAndVerifyAuthorize(_chargeTagId).Result)
                {
                    /* 7.  Now test complete transaction  */
                    string transactionId = Guid.NewGuid().ToString();
                    if (SendAndVerifyStartTransaction(1, _chargeTagId, transactionId).Result)
                    {
                        SendAndVerifyUpdateTransaction(1, _chargeTagId, transactionId).Wait();

                        SendAndVerifyStatusNotification(1, "Occupied").Wait();

                        /* 8.  Check chargepoint status in/from server  */
                        ReadServerStatus();

                        Console.WriteLine("Press enter to stop transaction");
                        Console.ReadLine();

                        /*
                        // KEBA sends status availabe before ending the transaction
                        SendAndVerifyStatusNotification(1, "Available").Wait();
                        ReadServerStatus();
                        Console.WriteLine("Status availabe with running transaction - Press enter to continue");
                        Console.ReadLine();
                        */

                        SendAndVerifyStopTransaction(1, _chargeTagId, transactionId).Wait();
                    }
                }

                ReadServerStatus();
                Console.WriteLine("Transaction ended - Press enter send StatusUpdate");
                Console.ReadLine();

                /* 9.  Update connector status  */
                SendAndVerifyStatusNotification(1, "Available").Wait();

                ReadServerStatus();

                Console.WriteLine("Simulation ended. Press enter to exit.");
                Console.ReadLine();

                /* 10.  Close connection  */
                _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Simulation end", CancellationToken.None).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Test: {ex.ToString()}");
                Console.ReadLine();
            }
        }

        private static async Task<bool> SendAndVerifyBootNotification()
        {
            var payload = new
            {
                chargingStation = new
                {
                    serialNumber = "CPSN12345",
                    model = "SimulatorModel",
                    modem = new
                    {
                        iccid = "",
                        imsi = ""
                    },
                    vendorName = "SimulatorVendor",
                    firmwareVersion = "1.0.0",
                },
                reason = "PowerUp"
            };
            var response = await SendMessage("BootNotification", payload);
            var responsePayload = response[2].ToObject<JObject>();
            if (responsePayload["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"BootNotification failed: Status {responsePayload["status"]}");
                return false;
            }
            Console.WriteLine("BootNotification successful: Status accepted");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyHeartbeat()
        {
            var payload = new { };
            var response = await SendMessage("Heartbeat", payload);
            var responsePayload = response[2].ToObject<JObject>();
            if (responsePayload["currentTime"] == null)
            {
                Console.WriteLine("Heartbeat failed: Answer contains no 'currentTime' field");
                return false;
            }
            Console.WriteLine("Heartbeat successful: CurrentTime received");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyAuthorize(string chargeTagId)
        {
            var payload = new
            {
                idToken = new
                {
                    idToken = chargeTagId,
                    type = "ISO14443"
                }                
            };
            var response = await SendMessage("Authorize", payload);
            var responsePayload = response[2].ToObject<JObject>();
            var idTagInfo = responsePayload["idTokenInfo"];
            if (idTagInfo["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"Authorize failed: Status {idTagInfo["status"]}");
                return false;
            }
            Console.WriteLine("Authorize successful: Status accepted");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyStartTransaction(int connectorId, string chargeTagId, string transactionId)
        {
            var payload = new
            {
                eventType = "Started",
                meterValue = new[]
                {
                    new
                    {
                        sampledValue = new[]
                        {
                            new
                            {
                                value = 5000,
                                context = "Transaction.Begin"
                            }
                        },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                },
                timestamp = DateTime.UtcNow.ToString("o"),
                triggerReason = "Authorized",
                seqNo = _seqNo++,
                offline = false,
                transactionInfo = new
                {
                    transactionId = transactionId,
                    chargingState = "SuspendedEV"
                },
                evse = new
                {
                    id = connectorId,
                    connectorId = 1
                },
                idToken = new
                {
                    idToken = chargeTagId,
                    type = "ISO14443"
                }
            };
            var response = await SendMessage("TransactionEvent", payload);
            var responsePayload = response[2].ToObject<JObject>();
            var idTagInfo = responsePayload["idTokenInfo"];
            if (idTagInfo["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"StartTransaction failed: Status {idTagInfo["status"]}");
                return false;
            }
            Console.WriteLine($"StartTransaction successful: transactionId {transactionId}");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyUpdateTransaction(int connectorId, string chargeTagId, string transactionId)
        {
            var payload = new
            {
                eventType = "Updated",
                meterValue = new[]
                {
                    new
                    {
                        sampledValue = new[]
                        {
                            new
                            {
                                value = 5000,
                                context = "Sample.Periodic",
                                measurand = "Energy.Active.Import.Register",
                                location = "Outlet",
                                unitOfMeasure = new
                                {
                                    unit = "Wh",
                                    multiplier = 0
                                }
                            }
                        },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                },
                timestamp = DateTime.UtcNow.ToString("o"),
                triggerReason = "MeterValuePeriodic",
                seqNo = _seqNo++,
                offline = false,
                transactionInfo = new
                {
                    transactionId = transactionId,
                    chargingState = "Idle",
                    stoppedReason = "EVDisconnected"
                },
                evse = new
                {
                    id = connectorId,
                    connectorId = 1
                },
                idToken = new
                {
                    idToken = chargeTagId,
                    type = "ISO14443"
                }
            };

            var response = await SendMessage("TransactionEvent", payload);
            var responsePayload = response[2].ToObject<JObject>();
            Console.WriteLine("UpdateTransaction successful");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyStopTransaction(int connectorId, string chargeTagId, string transactionId)
        {
            var payload = new
            {
                eventType = "Ended",
                meterValue = new[]
                {
                    new
                    {
                        sampledValue = new[]
                        {
                            new
                            {
                                value = 5000,
                                context = "Transaction.End",
                                measurand = "Energy.Active.Import.Register",
                                location = "Outlet",
                                unitOfMeasure = new
                                {
                                    unit = "Wh",
                                    multiplier = 0
                                }
                            }
                        },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                },
                timestamp = DateTime.UtcNow.ToString("o"),
                triggerReason = "Authorized",
                seqNo = _seqNo++,
                offline = false,
                transactionInfo = new
                {
                    transactionId = transactionId,
                    chargingState = "Idle",
                    stoppedReason = "EVDisconnected"
                },
                evse = new
                {
                    id = connectorId,
                    connectorId = 1
                },
                idToken = new
                {
                    idToken = chargeTagId,
                    type = "ISO14443"
                }
            };

            var response = await SendMessage("TransactionEvent", payload);
            var responsePayload = response[2].ToObject<JObject>();
            var idTagInfo = responsePayload["idTokenInfo"];
            if (idTagInfo != null && idTagInfo["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"StopTransaction failed: Status {idTagInfo["status"]}");
                return false;
            }
            Console.WriteLine("StopTransaction successful");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyMeterValues(int connectorId)
        {
            var payload = new
            {
                evseId = connectorId,
                meterValue = new[]
                {
                    new
                    {
                        sampledValue = new[]
                        {
                            new
                            {
                                value = 5000,
                                context = "Sample.Clock",
                                measurand = "Energy.Active.Import.Register",
                                location = "Outlet",
                                unitOfMeasure = new
                                {
                                    unit = "Wh",
                                    multiplier = 0
                                }
                            }
                        },
                        timestamp = DateTime.UtcNow.ToString("o"),
                    }
                }
            };
            var response = await SendMessage("MeterValues", payload);
            var responsePayload = response[2].ToObject<JObject>();
            Console.WriteLine("SendMeterValues successful");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyStatusNotification(int connectorId, string status)
        {
            var payload = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                connectorStatus = status,
                evseId = connectorId,
                connectorId = 1
            };
            var response = await SendMessage("StatusNotification", payload);
            var responsePayload = response[2].ToObject<JObject>();
            Console.WriteLine($"StatusNotification ({status}) successful");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyDataTransfer()
        {
            var payload = new
            {
                vendorId = "SimulatorVendor",
                messageId = "TestMessage",
                data = "TestData"
            };
            var response = await SendMessage("DataTransfer", payload);
            var responsePayload = response[2].ToObject<JObject>();
            if (responsePayload["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"DataTransfer failed: Status {responsePayload["status"]}");
                return false;
            }
            Console.WriteLine("DataTransfer successful: Status accepted");
            Console.WriteLine();
            return true;
        }
        
        private static async Task<bool> SendAndVerifyLogStatusNotification()
        {
            var payload = new
            {
                status = "Idle"
            };
            var response = await SendMessage("LogStatusNotification", payload);
            var responsePayload = response[2].ToObject<JObject>();
            Console.WriteLine("LogStatusNotification successful");
            Console.WriteLine();
            return true;
        }

        private static void ReadServerStatus()
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                HttpResponseMessage response = httpClient.GetAsync(new Uri(_serverUrl + "/API/Status")).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string jsonData = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"Server status JSON: {jsonData}");
                }
                else
                {
                    Console.WriteLine($"Server status request failed: httpStatus={response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server status request failed: {ex.ToString()}");
            }
            Console.WriteLine();
        }


        private static async Task SendCallResult(string uniqueId, object payload)
        {
            var message = new JArray
            {
                3, // CallResult
                uniqueId,
                JObject.FromObject(payload)
            };
            string json = JsonConvert.SerializeObject(message);
            Console.WriteLine($"Sende Result: {json}");
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
            await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }


        private static async Task<JArray> SendMessage(string action, object payload)
        {
            _responseTcs = new TaskCompletionSource<JArray>();
            string uniqueId = Guid.NewGuid().ToString();
            var message = new JArray
            {
                2, // Call
                uniqueId,
                action,
                JObject.FromObject(payload)
            };

            string json = JsonConvert.SerializeObject(message);
            Console.WriteLine($"Sending: {json}");

            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
            await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);

            // Waiting x seconds for an answer (timeout)
            int waitSeconds = 5;
            var timeoutTask = Task.Delay(waitSeconds * 1000);
            var completedTask = await Task.WhenAny(_responseTcs.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"No response for message {action} received in {waitSeconds} seconds.");
            }

            return await _responseTcs.Task;
        }

        private static async Task ReceiveMessages()
        {
            var buffer = new byte[4096];
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string received = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    Console.WriteLine($"Received: {received}");

                    try
                    {
                        var message = JsonConvert.DeserializeObject<JArray>(received);
                        if (message[0].Value<int>() == 3) // CallResult
                        {
                            // Result/Anwer => Send response via TCS to waiting code
                            _responseTcs?.TrySetResult(message);
                        }
                        else if (message[0].Value<int>() == 4) // CallError
                        {
                            _responseTcs?.TrySetException(new Exception($"Error response: {message[3]}"));
                        }
                        // Server-to-Client Calls (e.g. Reset, UnlockConnector) can be handled here
                        else if (message[0].Value<int>() == 2)
                        {
                            string action = message[2].ToString();
                            string uniqueId = message[1].ToString();
                            // Example: Process Reset
                            if (action == "Reset")
                            {
                                await SendCallResult(uniqueId, new { status = "Accepted" });
                            }
                            // further msg types...
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing incoming message: {ex.Message}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
            }
        }
    }
}
