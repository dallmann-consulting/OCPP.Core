using System.Diagnostics;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// OCPP-J frames (parser instead of regex), CALLERROR answers of the chargepoint,
    /// invalid messages, OCPP 2.1 message types 5/6
    /// </summary>
    public class RpcFrameTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;
        private readonly HttpClient _client;

        public RpcFrameTests(OcppServerFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private static string InvalidMessageErrorCode(string protocol) => (protocol == OcppProtocols.Ocpp16) ? "FormationViolation" : "RpcFrameworkError";

        private async Task<JObject> GetJsonAsync(string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        // ---------- CALLERROR of the chargepoint => API gets the error immediately ----------

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task CallErrorOfChargePoint_IsReturnedImmediately(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            // null => simulator answers with CALLERROR "NotImplemented"
            cp.ServerCallHandler = call => null;

            Stopwatch stopwatch = Stopwatch.StartNew();
            JObject result = await GetJsonAsync($"/API/Reset/{cp.ChargePointId}");

            // before: 60 s timeout and status "Timeout"
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Equal("CallError", result["status"]!.Value<string>());
            Assert.Equal("NotImplemented", result["errorCode"]!.Value<string>());
        }

        [Fact]
        public async Task CallErrorOfChargePoint_WithDescription_IsReturned()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp201);
            // own CALLERROR with description (contains quotes)
            cp.ServerCallHandler = call =>
            {
                _ = cp.SendTextAsync($"[4,\"{call.UniqueId}\",\"SecurityError\",\"Not \\\"allowed\\\" now\",{{}}]");
                return ChargePointSimulator.NoAnswer;
            };

            JObject result = await GetJsonAsync($"/API/UnlockConnector/{cp.ChargePointId}/1");

            Assert.Equal("CallError", result["status"]!.Value<string>());
            Assert.Equal("SecurityError", result["errorCode"]!.Value<string>());
            Assert.Equal("Not \"allowed\" now", result["errorDescription"]!.Value<string>());
        }

        [Fact]
        public async Task Ocpp16_GetVariables_CallError_IsVariableStatus()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            cp.ServerCallHandler = call => null;

            JObject single = await GetJsonAsync($"/API/GetVariables/{cp.ChargePointId}/HeartbeatInterval");
            JObject full = await GetJsonAsync($"/API/GetVariables/{cp.ChargePointId}");

            Assert.Equal("CallError", single["variables"]![0]!["status"]!.Value<string>());
            Assert.Equal("NotImplemented", single["variables"]![0]!["statusInfo"]!.Value<string>());
            Assert.Equal("CallError", full["status"]!.Value<string>());
        }

        [Fact]
        public async Task Ocpp16_SetVariables_CallErrorForOneKey_OtherKeysAreSent()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            cp.ServerCallHandler = call => (call.Payload["key"]!.Value<string>() == "BadKey") ? null : new { status = "Accepted" };

            string body = "{\"variables\":[{\"variable\":{\"name\":\"BadKey\"},\"value\":\"1\"},{\"variable\":{\"name\":\"HeartbeatInterval\"},\"value\":\"240\"}]}";
            HttpResponseMessage response = await _client.PostAsync($"/API/SetVariables/{cp.ChargePointId}", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            // a CALLERROR concerns only this key (timeout/disconnect would stop)
            Assert.Equal("CallError", result["variables"]![0]!["status"]!.Value<string>());
            Assert.Equal("Accepted", result["variables"]![1]!["status"]!.Value<string>());
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task Ocpp2x_GetVariables_CallError_IsVariableStatus(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            cp.ServerCallHandler = call => null;

            JObject result = await GetJsonAsync($"/API/GetVariables/{cp.ChargePointId}/OCPPCommCtrlr.HeartbeatInterval");

            Assert.Equal("CallError", result["variables"]![0]!["status"]!.Value<string>());
        }

        // ---------- invalid messages of the chargepoint ----------

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task InvalidJson_IsAnsweredWithCallErrorAndIdMinusOne(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult result = await cp.SendFrameAndWaitAsync("-1", "this is not json");

            Assert.True(result.IsError);
            Assert.Equal(InvalidMessageErrorCode(protocol), result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task CallWithoutPayload_IsAnsweredWithCallError(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult result = await cp.SendFrameAndWaitAsync("abc1", "[2,\"abc1\",\"Heartbeat\"]");

            Assert.True(result.IsError);
            Assert.Equal(InvalidMessageErrorCode(protocol), result.ErrorCode);
        }

        [Fact]
        public async Task CallWithTooManyElements_IsAnsweredWithCallError()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.SendFrameAndWaitAsync("abc2", "[2,\"abc2\",\"Heartbeat\",{},5]");

            Assert.True(result.IsError);
        }

        [Fact]
        public async Task UnknownMessageType_IsAnsweredWithCallError()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.SendFrameAndWaitAsync("abc3", "[9,\"abc3\",\"Heartbeat\",{}]");

            Assert.True(result.IsError);
        }

        // ---------- valid messages that the regex didn't handle ----------

        [Fact]
        public async Task MessageWithLineBreaksAndWhitespace_IsProcessed()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.SendFrameAndWaitAsync("ws1", "[ 2 ,\r\n  \"ws1\" ,\r\n  \"Heartbeat\" ,\r\n  { }\r\n]");

            Assert.False(result.IsError);
            Assert.NotNull(result.Payload!["currentTime"]);
        }

        [Fact]
        public async Task PayloadWithBracketsInString_IsProcessed()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.CallAsync("DataTransfer", new { vendorId = "Test", messageId = "x", data = "a}]b\"c" });

            Assert.False(result.IsError);
            Assert.Equal("Accepted", result.Payload!["status"]!.Value<string>());
        }

        [Fact]
        public async Task LateCallResult_IsIgnored()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            await cp.SendTextAsync("[3,\"unknownRequestId\",{\"status\":\"Accepted\"}]");
            await cp.SendTextAsync("[4,\"unknownRequestId\",\"GenericError\",\"\",{}]");

            OcppResult heartbeat = await cp.CallAsync("Heartbeat", new { });
            Assert.False(heartbeat.IsError);
        }

        // ---------- OCPP 2.1: CALLRESULTERROR (5) and SEND (6) ----------

        [Fact]
        public async Task Ocpp21_SendAndCallResultError_AreIgnoredWithoutAnswer()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp21);

            await cp.SendTextAsync("[6,\"send1\",\"NotifyPeriodicEventStream\",{\"id\":1,\"pending\":0,\"basetime\":\"2026-10-24T10:00:00Z\",\"data\":[]}]");
            await cp.SendTextAsync("[5,\"res1\",\"FormatViolation\",\"bad result\",{}]");
            // no answer to SEND => a CALL with the same id gets no other answer than its own
            await Assert.ThrowsAsync<TimeoutException>(() => cp.SendFrameAndWaitAsync("send1", "[6,\"send1\",\"NotifyPeriodicEventStream\",{}]", TimeSpan.FromSeconds(1)));

            OcppResult heartbeat = await cp.CallAsync("Heartbeat", new { });
            Assert.False(heartbeat.IsError);
        }

        // ---------- frames of the server ----------

        [Fact]
        public async Task ServerFrames_AreValidJson()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            // CALLERROR of the server (unknown action) => 5 elements
            OcppResult error = await cp.CallAsync("Foo\"Bar", new { });

            Assert.True(error.IsError);
            Assert.Equal("NotSupported", error.ErrorCode);
        }
    }
}
