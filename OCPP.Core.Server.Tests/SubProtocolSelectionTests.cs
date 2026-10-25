using System.Net.WebSockets;
using Microsoft.AspNetCore.TestHost;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// The order of the sub-protocols offered by the charge station is its preference
    /// (OCPP 2.0.1 Part 4, chapter 3.1: e.g. "ocpp2.0.1, ocpp1.6" => 2.0.1)
    /// </summary>
    public class SubProtocolSelectionTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public SubProtocolSelectionTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("ocpp2.0.1,ocpp1.6", "ocpp2.0.1")]
        [InlineData("ocpp2.1,ocpp2.0.1,ocpp1.6", "ocpp2.1")]
        [InlineData("ocpp1.6,ocpp2.0.1", "ocpp1.6")]
        [InlineData("ocpp1.6,ocpp2.1", "ocpp1.6")]
        // unsupported protocols are skipped
        [InlineData("ocpp1.5,ocpp2.1,ocpp1.6", "ocpp2.1")]
        public async Task FirstSupportedProtocolOfTheStation_IsSelected(string offered, string expected)
        {
            string chargePointId = _factory.AddChargePoint();
            WebSocketClient client = _factory.Server.CreateWebSocketClient();
            foreach (string protocol in offered.Split(','))
            {
                client.SubProtocols.Add(protocol);
            }

            using WebSocket webSocket = await client.ConnectAsync(new Uri(_factory.Server.BaseAddress, "OCPP/" + chargePointId), TestContext.Current.CancellationToken);

            Assert.Equal(expected, webSocket.SubProtocol);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task NoSupportedProtocol_IsRejected()
        {
            string chargePointId = _factory.AddChargePoint();
            WebSocketClient client = _factory.Server.CreateWebSocketClient();
            client.SubProtocols.Add("ocpp1.5");
            client.SubProtocols.Add("ocpp1.2");

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ConnectAsync(new Uri(_factory.Server.BaseAddress, "OCPP/" + chargePointId), TestContext.Current.CancellationToken));

            Assert.Contains("400", exp.Message);
        }
    }
}
