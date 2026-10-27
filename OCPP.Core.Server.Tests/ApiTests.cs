using System.Net;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// REST API of the server (without API key)
    /// </summary>
    public class ApiTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;
        private readonly HttpClient _client;

        public ApiTests(OcppServerFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private static string StartTransactionAction(string protocol) => (protocol == OcppProtocols.Ocpp16) ? "RemoteStartTransaction" : "RequestStartTransaction";

        [Fact]
        public async Task Status_ContainsConnectedChargePoint()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp201);

            HttpResponseMessage response = await _client.GetAsync("/API/Status", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JArray status = JArray.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            JToken? entry = status.FirstOrDefault(s => s["id"]?.Value<string>() == cp.ChargePointId);
            Assert.NotNull(entry);
            Assert.Equal(OcppProtocols.Ocpp201, entry["protocol"]!.Value<string>());
        }

        [Fact]
        public async Task UnknownCommand_ReturnsNotFound()
        {
            HttpResponseMessage response = await _client.GetAsync("/API/FooBar", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task Reset_IsSentToChargePoint(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            HttpResponseMessage response = await _client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Accepted", result["status"]!.Value<string>());
            Assert.Contains(cp.ReceivedCalls, c => c.Action == "Reset");
        }

        [Fact]
        public async Task Reset_OfflineChargePoint_ReturnsNotFound()
        {
            string chargePointId = _factory.AddChargePoint();

            HttpResponseMessage response = await _client.GetAsync($"/API/Reset/{chargePointId}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task UnlockConnector_IsSentToChargePoint(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            cp.ServerCallHandler = call => new { status = "Unlocked" };

            HttpResponseMessage response = await _client.GetAsync($"/API/UnlockConnector/{cp.ChargePointId}/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            ServerCall call = await cp.WaitForServerCallAsync("UnlockConnector");
            string idField = (protocol == OcppProtocols.Ocpp16) ? "connectorId" : "evseId";
            Assert.Equal(1, call.Payload[idField]!.Value<int>());
            if (protocol != OcppProtocols.Ocpp16)
            {
                // OCPP 2.x requires the connector within the EVSE (0 doesn't exist)
                Assert.Equal(1, call.Payload["connectorId"]!.Value<int>());
            }
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task StartTransaction_KnownTag_IsSentToChargePoint(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            HttpResponseMessage response = await _client.GetAsync($"/API/StartTransaction/{cp.ChargePointId}/1/{tagId}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Accepted", result["status"]!.Value<string>());
            ServerCall call = await cp.WaitForServerCallAsync(StartTransactionAction(protocol));
            string sentTag = (protocol == OcppProtocols.Ocpp16) ? call.Payload["idTag"]!.Value<string>()! : call.Payload["idToken"]!["idToken"]!.Value<string>()!;
            Assert.Equal(tagId, sentTag);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task StartTransaction_UnknownTag_IsRejectedWithoutChargePointCall(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            HttpResponseMessage response = await _client.GetAsync($"/API/StartTransaction/{cp.ChargePointId}/1/UNKNOWNTAG", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Rejected", result["status"]!.Value<string>());
            Assert.DoesNotContain(cp.ReceivedCalls, c => c.Action == StartTransactionAction(protocol));
        }

        [Fact]
        public async Task StopTransaction_WithoutOpenTransaction_ReturnsFailedDependency()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            HttpResponseMessage response = await _client.GetAsync($"/API/StopTransaction/{cp.ChargePointId}/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.FailedDependency, response.StatusCode);
        }

        [Fact]
        public async Task StopTransaction_OpenTransaction_IsSentToChargePoint()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            OcppResult start = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 0, timestamp = DateTimeOffset.UtcNow });
            int transactionId = start.Payload!["transactionId"]!.Value<int>();

            HttpResponseMessage response = await _client.GetAsync($"/API/StopTransaction/{cp.ChargePointId}/1", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            ServerCall call = await cp.WaitForServerCallAsync("RemoteStopTransaction");
            Assert.Equal(transactionId, call.Payload["transactionId"]!.Value<int>());
        }
    }

    /// <summary>
    /// Server with configured API key
    /// </summary>
    public class ApiKeyServerFactory : OcppServerFactory
    {
        public const string ApiKey = "test-api-key-0123456789";

        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["ApiKey"] = ApiKey;
        }
    }

    /// <summary>
    /// REST API authentication with API key
    /// </summary>
    public class ApiKeyTests : IClassFixture<ApiKeyServerFactory>
    {
        private readonly HttpClient _client;

        public ApiKeyTests(ApiKeyServerFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task WithoutApiKey_IsUnauthorized()
        {
            HttpResponseMessage response = await _client.GetAsync("/API/Status", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithWrongApiKey_IsUnauthorized()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/API/Status");
            request.Headers.Add("X-API-Key", "wrong-key");

            HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task WithCorrectApiKey_IsOk()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/API/Status");
            request.Headers.Add("X-API-Key", ApiKeyServerFactory.ApiKey);

            HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
