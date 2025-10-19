using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OCPP.Core.Test
{
    class OCPP16Test
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

        internal static void Execute()
        {
            Console.WriteLine("Press enter to start test with OCPP 1.6");
            Console.ReadLine();

            try
            {

                /* 1.  Try to connect with an unknown chargepoint id => it should fail  */
                try
                {
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.AddSubProtocol("ocpp1.6"); // OCPP 1.6J

                    Console.WriteLine($"Connecting with unknown chargepoint: {_serverUrl + "/OCPP/unknown" + _chargePointId}");
                    _webSocket.ConnectAsync(new Uri(_serverUrl + "/OCPP/unknown" + _chargePointId), CancellationToken.None).Wait();
                    Console.WriteLine("BAD: Connected successful with unknown chargepoint");
                }
                catch
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
                _webSocket.Options.AddSubProtocol("ocpp1.6"); // OCPP 1.6J

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
                    int transactionId = SendAndVerifyStartTransaction(1, _chargeTagId).Result;
                    if (transactionId >= 0)
                    {
                        SendAndVerifyMeterValues(1, transactionId).Wait();

                        SendAndVerifyStatusNotification(1, "Charging").Wait();

                        /* 8.  Check chargepoint status in/from server  */
                        ReadServerStatus();

                        Console.WriteLine("Press enter to stop transaction");
                        Console.ReadLine();

                        SendAndVerifyStopTransaction(1, transactionId).Wait();
                    }
                }


                /* 9.  Update connector status  */
                SendAndVerifyStatusNotification(1, "Available").Wait();

                ReadServerStatus();


                /* 10.  Server API calls  */
                SendReset(_chargePointId);
                SendUnlock(_chargePointId, 1);
                SetChargingLimit(_chargePointId, 1, "1000W");
                ClearChargingLimit(_chargePointId, 1);


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
                chargePointVendor = "SimulatorVendor",
                chargePointModel = "SimulatorModel",
                chargePointSerialNumber = "CPSN12345",
                chargeBoxSerialNumber = "CBSN12345",
                firmwareVersion = "1.0.0",
                iccid = "",
                imsi = "",
                meterType = "SimulatorMeter",
                meterSerialNumber = "Serial12345"
            };
            var response = await SendMessage("BootNotification", payload);
            var responsePayload = response[2].ToObject<JObject>();
            if (responsePayload?["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"BootNotification failed: Status {responsePayload?["status"]}");
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
            if (responsePayload?["currentTime"] == null)
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
                idTag = chargeTagId
            };
            var response = await SendMessage("Authorize", payload);
            var responsePayload = response[2].ToObject<JObject>();
            var idTagInfo = responsePayload?["idTagInfo"];
            if (idTagInfo?["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"Authorize failed: Status {idTagInfo?["status"]}");
                return false;
            }
            Console.WriteLine("Authorize successful: Status accepted");
            Console.WriteLine();
            return true;
        }

        private static async Task<int> SendAndVerifyStartTransaction(int connectorId, string chargeTagId)
        {
            var payload = new
            {
                connectorId = connectorId,
                idTag = chargeTagId,
                meterStart = 0,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            var response = await SendMessage("StartTransaction", payload);
            var responsePayload = response[2].ToObject<JObject>();
            var idTagInfo = responsePayload?["idTagInfo"];
            if (idTagInfo?["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"StartTransaction failed: Status {idTagInfo?["status"]}");
                return -1;
            }
            int transactionId = responsePayload?["transactionId"]?.Value<int>() ?? throw new Exception("No transactionId in StartTransaction answer");
            Console.WriteLine($"StartTransaction successful: transactionId {transactionId}");
            Console.WriteLine();
            return transactionId;
        }

        private static async Task<bool> SendAndVerifyStopTransaction(int connectorId, int transactionId)
        {
            var payload = new
            {
                idTag = _chargeTagId,
                meterStop = 10000,
                timestamp = DateTime.UtcNow.ToString("o"),
                transactionId = transactionId,
                reason = "Local",
            };
            var response = await SendMessage("StopTransaction", payload);
            var responsePayload = response[2].ToObject<JObject>();
            var idTagInfo = responsePayload?["idTagInfo"];
            if (idTagInfo != null && idTagInfo["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"StopTransaction failed: Status {idTagInfo["status"]}");
                return false;
            }
            Console.WriteLine("StopTransaction successful");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyMeterValues(int connectorId, int transactionId)
        {
            var payload = new
            {
                connectorId = connectorId,
                transactionId = transactionId,
                meterValue = new[]
                {
                    new
                    {
                        timestamp = DateTime.UtcNow.ToString("o"),
                        sampledValue = new[]
                        {
                            new
                            {
                                value = "5000",
                                context = "Sample.Periodic",
                                format = "Raw",
                                measurand = "Energy.Active.Import.Register",
                                location = "Outlet",
                                unit = "Wh"
                            }
                        }
                    }
                }
            };
            var response = await SendMessage("MeterValues", payload);
            var responsePayload = response[2].ToObject<JObject>();
            // answer should be empty {}
            if (responsePayload?.Count > 0)
            {
                Console.WriteLine("MeterValues answer contains unexpected data");
                return false;
            }
            Console.WriteLine("SendMeterValues successful");
            Console.WriteLine();
            return true;
        }

        private static async Task<bool> SendAndVerifyStatusNotification(int connectorId, string status)
        {
            var payload = new
            {
                connectorId = connectorId,
                errorCode = "NoError",
                status = status,
                timestamp = DateTime.UtcNow.ToString("o"),
                info = "",
                vendorId = "",
                vendorErrorCode = ""
            };
            var response = await SendMessage("StatusNotification", payload);
            var responsePayload = response[2].ToObject<JObject>();
            // answer should be empty {}
            if (responsePayload?.Count > 0)
            {
                Console.WriteLine("StatusNotification answer contains unexpected data");
                return false;
            }
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
            if (responsePayload?["status"]?.ToString() != "Accepted")
            {
                Console.WriteLine($"DataTransfer failed: Status {responsePayload?["status"]}");
                return false;
            }
            Console.WriteLine("DataTransfer successful: Status accepted");
            Console.WriteLine();
            return true;
        }

        private static void SendReset(string chargePointId)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                HttpResponseMessage response = httpClient.GetAsync(new Uri(_serverUrl + "/API/Reset/" + chargePointId)).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    if (result.Contains("\"Accepted\""))
                    {
                        Console.WriteLine($"Success: API Reset result JSON: {result}");
                    }
                    else
                    {
                        Console.WriteLine($"Failure: API Reset result JSON: {result}");
                    }
                }
                else
                {
                    Console.WriteLine($"Reset API request failed: httpStatus={response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reset API request failed: {ex.ToString()}");
            }
            Console.WriteLine();
        }

        private static void SendUnlock(string chargePointId, int connectorId)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                HttpResponseMessage response = httpClient.GetAsync(new Uri($"{_serverUrl}/API/UnlockConnector/{chargePointId}/{connectorId}")).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    if (result.Contains("\"Unlocked"))
                    {
                        Console.WriteLine($"Success: API Unlock result JSON: {result}");
                    }
                    else
                    {
                        Console.WriteLine($"Failure: API Unlock result JSON: {result}");
                    }
                }
                else
                {
                    Console.WriteLine($"Unlock API request failed: httpStatus={response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unlock API request failed: {ex.ToString()}");
            }
            Console.WriteLine();
        }

        private static void SetChargingLimit(string chargePointId, int connectorId, string pwLimit)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                HttpResponseMessage response = httpClient.GetAsync(new Uri($"{_serverUrl}/API/SetChargingLimit/{chargePointId}/{connectorId}/{pwLimit}")).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    if (result.Contains("\"Accepted\""))
                    {
                        Console.WriteLine($"Success: API SetChargingLimit result JSON: {result}");
                    }
                    else
                    {
                        Console.WriteLine($"Failure: API SetChargingLimit result JSON: {result}");
                    }
                }
                else
                {
                    Console.WriteLine($"SetChargingLimit API request failed: httpStatus={response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetChargingLimit API request failed: {ex.ToString()}");
            }
            Console.WriteLine();
        }

        private static void ClearChargingLimit(string chargePointId, int connectorId)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
                HttpResponseMessage response = httpClient.GetAsync(new Uri($"{_serverUrl}/API/ClearChargingLimit/{chargePointId}/{connectorId}")).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    if (result.Contains("\"Accepted\""))
                    {
                        Console.WriteLine($"Success: API ClearChargingLimit result JSON: {result}");
                    }
                    else
                    {
                        Console.WriteLine($"Failure: API ClearChargingLimit result JSON: {result}");
                    }
                }
                else
                {
                    Console.WriteLine($"ClearChargingLimit API request failed: httpStatus={response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClearChargingLimit API request failed: {ex.ToString()}");
            }
            Console.WriteLine();
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
            Console.WriteLine($"Sending answer: {json}");
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
                        if (message?[0].Value<int>() == 3) // CallResult
                        {
                            // Result/Anwer => Send response via TCS to waiting code
                            _responseTcs?.TrySetResult(message);
                        }
                        else if (message?[0].Value<int>() == 4) // CallError
                        {
                            _responseTcs?.TrySetException(new Exception($"Error response: {message[3]}"));
                        }
                        // Server-to-Client Calls (e.g. Reset, UnlockConnector) can be handled here
                        else if (message?[0].Value<int>() == 2)
                        {
                            string action = message[2].ToString();
                            string uniqueId = message[1].ToString();
                            switch (action)
                            {
                                case "Reset":
                                    await SendCallResult(uniqueId, new { status = "Accepted" });
                                    break;
                                case "UnlockConnector":
                                    await SendCallResult(uniqueId, new { status = "Unlocked" });
                                    break;
                                case "SetChargingProfile":
                                    await SendCallResult(uniqueId, new { status = "Accepted" });
                                    break;
                                case "ClearChargingProfile":
                                    await SendCallResult(uniqueId, new { status = "Accepted" });
                                    break;
                                default:
                                    Console.WriteLine($"Error: Unknown incoming message: {action}");
                                    break;
                            }
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
