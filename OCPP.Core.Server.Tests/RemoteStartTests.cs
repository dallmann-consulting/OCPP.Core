using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// OCPP 2.x remote start: unique "remoteStartId" (assignment of the started transaction to the command in the EventLog)
    /// and the idToken type of the remote start
    /// </summary>
    public class RemoteStartTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public RemoteStartTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task RemoteStartId_IsUniqueAndShownInTheEventLog(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);
            HttpClient client = _factory.CreateClient();

            await client.GetAsync($"/API/StartTransaction/{chargePointId}/1/{tagId}", TestContext.Current.CancellationToken);
            await client.GetAsync($"/API/StartTransaction/{chargePointId}/1/{tagId}", TestContext.Current.CancellationToken);

            List<ServerCall> calls = cp.ReceivedCalls.Where(c => c.Action == "RequestStartTransaction").ToList();
            Assert.Equal(2, calls.Count);
            int first = calls[0].Payload["remoteStartId"]!.Value<int>();
            int second = calls[1].Payload["remoteStartId"]!.Value<int>();
            Assert.NotEqual(0, first);
            Assert.NotEqual(first, second);
            // default type: the tags are RFID cards
            Assert.Equal("ISO14443", calls[0].Payload.SelectToken("idToken.type")!.Value<string>());

            // the charge point starts the transaction with the ID of the command
            string uid = Guid.NewGuid().ToString();
            await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Started",
                timestamp = DateTimeOffset.UtcNow,
                triggerReason = "RemoteStart",
                seqNo = 0,
                transactionInfo = new { transactionId = uid, remoteStartId = second },
                evse = new { id = 1, connectorId = 1 },
                idToken = new { idToken = tagId, type = "ISO14443" }
            });

            using OCPPCoreContext db = _factory.CreateDbContext();
            List<ChargePointEvent> commands = db.ChargePointEvents.AsNoTracking()
                .Where(e => e.ChargePointId == chargePointId && e.EventCode == EventCode.Command)
                .OrderBy(e => e.EventId).ToList();
            Assert.Equal(new[] { first.ToString(), second.ToString() }, commands.Select(e => EventLogExtensions.ReadParameters(e.Parameters)["remoteStartId"]));

            ChargePointEvent started = db.ChargePointEvents.AsNoTracking().Single(e => e.ChargePointId == chargePointId && e.EventCode == EventCode.TransactionStarted);
            Assert.Equal(second.ToString(), EventLogExtensions.ReadParameters(started.Parameters)["remoteStartId"]);
        }
    }

    /// <summary>
    /// Server with another idToken type for remote starts
    /// </summary>
    public class RemoteStartIdTokenTypeServerFactory : OcppServerFactory
    {
        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["RemoteStartIdTokenType"] = "ISO15693";
        }
    }

    public class RemoteStartIdTokenTypeTests : IClassFixture<RemoteStartIdTokenTypeServerFactory>
    {
        private readonly RemoteStartIdTokenTypeServerFactory _factory;

        public RemoteStartIdTokenTypeTests(RemoteStartIdTokenTypeServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task ConfiguredIdTokenType_IsUsed(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            await _factory.CreateClient().GetAsync($"/API/StartTransaction/{chargePointId}/1/{tagId}", TestContext.Current.CancellationToken);

            ServerCall call = await cp.WaitForServerCallAsync("RequestStartTransaction");
            Assert.Equal("ISO15693", call.Payload.SelectToken("idToken.type")!.Value<string>());
        }
    }
}
