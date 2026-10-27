using System.Net.WebSockets;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// WebSocket handshake: charge point identification, authentication, sub protocols
    /// </summary>
    public class ConnectionTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public ConnectionTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task KnownChargePoint_Connects(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            Assert.Equal(WebSocketState.Open, cp.State);
            Assert.Equal(protocol, cp.Protocol);
        }

        [Fact]
        public async Task UnknownChargePoint_IsRejected()
        {
            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync("unknown-" + Guid.NewGuid().ToString("N"), OcppProtocols.Ocpp16));

            // OCPP-J chapter 3.2: unknown charge point => 404
            Assert.Contains("404", exp.Message);
        }

        [Fact]
        public async Task UnsupportedSubProtocol_IsRejected()
        {
            string chargePointId = _factory.AddChargePoint();

            // OCPP-J: handshake without sub-protocol, then the server closes the connection immediately
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, "ocpp1.5");

            Assert.True(string.IsNullOrEmpty(cp.Protocol));
            for (int i = 0; i < 100 && cp.State == WebSocketState.Open; i++)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
            Assert.NotEqual(WebSocketState.Open, cp.State);
        }

        [Fact]
        public async Task BasicAuth_WithoutCredentials_IsRejected()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16));

            Assert.Contains("401", exp.Message);
        }

        [Fact]
        public async Task BasicAuth_WithWrongPassword_IsRejected()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "wrong-password"));

            Assert.Contains("401", exp.Message);
        }

        [Fact]
        public async Task BasicAuth_WithCorrectCredentials_Connects()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "secret-password-123");

            Assert.Equal(WebSocketState.Open, cp.State);
        }

        [Fact]
        public async Task Reconnect_AfterClose_Connects()
        {
            string chargePointId = _factory.AddChargePoint();

            ChargePointSimulator first = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);
            await first.CallAsync("Heartbeat", new { });
            await first.DisposeAsync();

            // the server removes the old connection asynchronously => retry for a short time
            ChargePointSimulator? second = null;
            await Eventually.TrueAsync(() =>
            {
                try
                {
                    second = _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201).GetAwaiter().GetResult();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });

            Assert.NotNull(second);
            await using (second)
            {
                OcppResult result = await second.CallAsync("Heartbeat", new { });
                Assert.False(result.IsError);
            }
        }
    }
}
