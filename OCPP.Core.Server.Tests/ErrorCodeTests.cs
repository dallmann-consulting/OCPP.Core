using System.Diagnostics;
using System.Net;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Error codes of the OCPP version (1.6 "FormationViolation", 2.x "FormatViolation"),
    /// uniform for invalid messages in all handlers.
    /// An answer of the chargepoint that can't be processed ends the API request immediately.
    /// </summary>
    public class ErrorCodeTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public ErrorCodeTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private static string FormatErrorCode(string protocol) => (protocol == OcppProtocols.Ocpp16) ? "FormationViolation" : "FormatViolation";

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task InvalidStatusNotification_IsFormatError(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            // additional property => schema validation fails (before: "InternalError")
            OcppResult result = (protocol == OcppProtocols.Ocpp16)
                ? await cp.CallAsync("StatusNotification", new { connectorId = 1, errorCode = "NoError", status = "Available", foo = 1 })
                : await cp.CallAsync("StatusNotification", new { timestamp = DateTimeOffset.UtcNow, connectorStatus = "Available", evseId = 1, connectorId = 1, foo = 1 });

            Assert.True(result.IsError);
            Assert.Equal(FormatErrorCode(protocol), result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task InvalidMeterValues_IsFormatError(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            // "meterValue" is missing
            OcppResult result = (protocol == OcppProtocols.Ocpp16)
                ? await cp.CallAsync("MeterValues", new { connectorId = 1 })
                : await cp.CallAsync("MeterValues", new { evseId = 1 });

            Assert.True(result.IsError);
            Assert.Equal(FormatErrorCode(protocol), result.ErrorCode);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task InvalidTransactionEvent_IsFormatViolation(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            // "seqNo" is missing
            OcppResult result = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Started",
                timestamp = DateTimeOffset.UtcNow,
                triggerReason = "Authorized",
                transactionInfo = new { transactionId = Guid.NewGuid().ToString() }
            });

            Assert.True(result.IsError);
            Assert.Equal("FormatViolation", result.ErrorCode);
        }

        [Fact]
        public async Task Ocpp16_InvalidStartTransaction_IsFormationViolation()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = "X", meterStart = 0 });

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }


        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task InvalidAnswerOfChargePoint_EndsApiRequestImmediately(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            // CALLRESULT with invalid content
            cp.ServerCallHandler = call => new { status = "Bogus" };
            HttpClient client = _factory.CreateClient();

            Stopwatch stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response = await client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);

            // before: 60 s timeout and status "Timeout"
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("InvalidAnswer", result["status"]!.Value<string>());
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task InvalidGetVariablesAnswer_IsVariableStatus(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            cp.ServerCallHandler = call => new { foo = "bar" };
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync($"/API/GetVariables/{cp.ChargePointId}/OCPPCommCtrlr.HeartbeatInterval", TestContext.Current.CancellationToken);

            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("InvalidAnswer", result["variables"]![0]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task ValidAnswerAfterInvalidAnswer_IsProcessed()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            HttpClient client = _factory.CreateClient();

            cp.ServerCallHandler = call => new { status = "Bogus" };
            await client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);
            cp.ServerCallHandler = call => new { status = "Accepted" };
            HttpResponseMessage response = await client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);

            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Accepted", result["status"]!.Value<string>());
        }
    }
}
