using System.Net;
using System.Net.WebSockets;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// API without key only for local callers
    /// </summary>
    public class ApiWithoutKeyTests : IClassFixture<OcppServerFactory>
    {
        private readonly HttpClient _client;

        public ApiWithoutKeyTests(OcppServerFactory factory)
        {
            _client = factory.CreateClient();
        }

        private Task<HttpResponseMessage> GetStatusAsync(string? remoteIp, params (string Name, string Value)[] headers)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/API/Status");
            if (remoteIp != null) request.Headers.Add(RemoteIpStartupFilter.HeaderName, remoteIp);
            foreach ((string name, string value) in headers)
            {
                request.Headers.Add(name, value);
            }
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("::1")]
        [InlineData("::ffff:127.0.0.1")]
        public async Task LocalCaller_IsAllowed(string remoteIp)
        {
            HttpResponseMessage response = await GetStatusAsync(remoteIp);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("192.168.1.50")]
        [InlineData("172.17.0.1")]
        [InlineData("2001:db8::1")]
        public async Task RemoteCaller_IsRejected(string remoteIp)
        {
            HttpResponseMessage response = await GetStatusAsync(remoteIp);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("X-Forwarded-For", "203.0.113.7")]
        [InlineData("Forwarded", "for=203.0.113.7")]
        public async Task LocalReverseProxy_IsRejected(string headerName, string headerValue)
        {
            // reverse proxy on the same computer => loopback, but the request comes from the network
            HttpResponseMessage response = await GetStatusAsync("127.0.0.1", (headerName, headerValue));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    /// <summary>
    /// Server without API key and "AllowUnauthenticatedApi" (old behavior)
    /// </summary>
    public class AllowUnauthenticatedApiServerFactory : OcppServerFactory
    {
        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["AllowUnauthenticatedApi"] = "true";
        }
    }

    public class AllowUnauthenticatedApiTests : IClassFixture<AllowUnauthenticatedApiServerFactory>
    {
        private readonly HttpClient _client;

        public AllowUnauthenticatedApiTests(AllowUnauthenticatedApiServerFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RemoteCaller_IsAllowed()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/API/Status");
            request.Headers.Add(RemoteIpStartupFilter.HeaderName, "192.168.1.50");

            HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>
    /// Server with API key: the key is required for every caller (also local)
    /// </summary>
    public class ApiKeyCallerTests : IClassFixture<ApiKeyServerFactory>
    {
        private readonly HttpClient _client;

        public ApiKeyCallerTests(ApiKeyServerFactory factory)
        {
            _client = factory.CreateClient();
        }

        private Task<HttpResponseMessage> GetStatusAsync(string remoteIp, string? apiKey)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/API/Status");
            request.Headers.Add(RemoteIpStartupFilter.HeaderName, remoteIp);
            if (apiKey != null) request.Headers.Add("X-API-Key", apiKey);
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task RemoteCaller_WithCorrectKey_IsAllowed()
        {
            HttpResponseMessage response = await GetStatusAsync("192.168.1.50", ApiKeyServerFactory.ApiKey);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LocalCaller_WithoutKey_IsRejected()
        {
            HttpResponseMessage response = await GetStatusAsync("127.0.0.1", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("test-api-key")]                      // prefix of the real key
        [InlineData("test-api-key-0123456789-and-more")]  // real key with suffix
        [InlineData("TEST-API-KEY-0123456789")]           // other case
        public async Task WrongKey_IsRejected(string apiKey)
        {
            HttpResponseMessage response = await GetStatusAsync("192.168.1.50", apiKey);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    /// <summary>
    /// Server with "RequireChargePointAuthentication"
    /// </summary>
    public class RequireChargePointAuthenticationServerFactory : OcppServerFactory
    {
        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["RequireChargePointAuthentication"] = "true";
        }
    }

    public class RequireChargePointAuthenticationTests : IClassFixture<RequireChargePointAuthenticationServerFactory>
    {
        private readonly RequireChargePointAuthenticationServerFactory _factory;

        public RequireChargePointAuthenticationTests(RequireChargePointAuthenticationServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task ChargePointWithoutCredentials_IsRejected(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, protocol));

            Assert.Contains("401", exp.Message);
        }

        [Fact]
        public async Task ChargePointWithCredentials_Connects()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201, "user1", "secret-password-123");

            Assert.Equal(WebSocketState.Open, cp.State);
        }
    }
}
