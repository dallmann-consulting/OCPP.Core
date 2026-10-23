using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Messages from an OCPP 2.0.1 charge point to the server
    /// </summary>
    public class Ocpp201Tests : Ocpp2xTests
    {
        public Ocpp201Tests(OcppServerFactory factory) : base(factory, OcppProtocols.Ocpp201)
        {
        }
    }

    /// <summary>
    /// Messages from an OCPP 2.1 charge point to the server
    /// </summary>
    public class Ocpp21Tests : Ocpp2xTests
    {
        public Ocpp21Tests(OcppServerFactory factory) : base(factory, OcppProtocols.Ocpp21)
        {
        }
    }

    /// <summary>
    /// Common tests for OCPP 2.0.1 and 2.1 (same message structure for the tested messages)
    /// </summary>
    public abstract class Ocpp2xTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;
        private readonly string _protocol;

        protected Ocpp2xTests(OcppServerFactory factory, string protocol)
        {
            _factory = factory;
            _protocol = protocol;
        }

        private async Task<ChargePointSimulator> ConnectNewChargePointAsync()
        {
            return await _factory.ConnectAsync(_factory.AddChargePoint(), _protocol);
        }

        private static object IdToken(string tagId) => new { idToken = tagId, type = "ISO14443" };

        private static object[] EnergyMeterValue(DateTimeOffset timestamp, double wh) => new object[]
        {
            new
            {
                timestamp,
                sampledValue = new[]
                {
                    new { value = wh, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } }
                }
            }
        };

        [Fact]
        public async Task BootNotification_IsAccepted()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("BootNotification", new
            {
                reason = "PowerUp",
                chargingStation = new { model = "Simulator", vendorName = "Test" }
            });

            Assert.False(result.IsError);
            Assert.Equal("Accepted", result.Payload!["status"]!.Value<string>());
            Assert.Equal(300, result.Payload["interval"]!.Value<int>());
        }

        [Fact]
        public async Task Heartbeat_ReturnsCurrentTime()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Heartbeat", new { });

            Assert.False(result.IsError);
            Assert.NotNull(result.Payload!["currentTime"]);
        }

        [Fact]
        public async Task StatusNotification_UpdatesConnectorStatus()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("StatusNotification", new { timestamp = DateTimeOffset.UtcNow, connectorStatus = "Occupied", evseId = 1, connectorId = 1 });

            Assert.False(result.IsError);
            using OCPPCoreContext db = _factory.CreateDbContext();
            ConnectorStatus? status = db.ConnectorStatuses.Find(cp.ChargePointId, 1);
            Assert.NotNull(status);
            Assert.Equal("Occupied", status.LastStatus);
        }

        [Fact]
        public async Task Authorize_KnownTag_IsAccepted()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Authorize", new { idToken = IdToken(tagId) });

            Assert.Equal("Accepted", result.Payload!["idTokenInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Authorize_UnknownTag_IsNotAccepted()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Authorize", new { idToken = IdToken("UNKNOWN" + Guid.NewGuid().ToString("N").Substring(0, 8)) });

            Assert.False(result.IsError);
            Assert.NotEqual("Accepted", result.Payload!["idTokenInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Authorize_BlockedTag_IsBlocked()
        {
            string tagId = _factory.AddChargeTag(blocked: true);
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Authorize", new { idToken = IdToken(tagId) });

            Assert.Equal("Blocked", result.Payload!["idTokenInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Transaction_StartedUpdatedEnded_IsStoredInDatabase()
        {
            string tagId = _factory.AddChargeTag();
            string transactionUid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            OcppResult started = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Started",
                timestamp = start,
                triggerReason = "Authorized",
                seqNo = 0,
                transactionInfo = new { transactionId = transactionUid },
                idToken = IdToken(tagId),
                evse = new { id = 1, connectorId = 1 },
                meterValue = EnergyMeterValue(start, 1000)
            });
            Assert.False(started.IsError);
            Assert.Equal("Accepted", started.Payload!["idTokenInfo"]!["status"]!.Value<string>());

            OcppResult updated = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Updated",
                timestamp = start.AddMinutes(10),
                triggerReason = "MeterValuePeriodic",
                seqNo = 1,
                transactionInfo = new { transactionId = transactionUid, chargingState = "Charging" },
                evse = new { id = 1, connectorId = 1 },
                meterValue = EnergyMeterValue(start.AddMinutes(10), 4000)
            });
            Assert.False(updated.IsError);

            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                Transaction transaction = db.Transactions.Single(t => t.Uid == transactionUid);
                Assert.Equal(4.0, transaction.MeterStop);
                Assert.Null(transaction.StopTime);
            }

            OcppResult ended = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Ended",
                timestamp = start.AddMinutes(30),
                triggerReason = "StopAuthorized",
                seqNo = 2,
                transactionInfo = new { transactionId = transactionUid, stoppedReason = "Local" },
                idToken = IdToken(tagId),
                evse = new { id = 1, connectorId = 1 },
                meterValue = EnergyMeterValue(start.AddMinutes(30), 12500)
            });
            Assert.False(ended.IsError);
            Assert.Equal("Accepted", ended.Payload!["idTokenInfo"]!["status"]!.Value<string>());

            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                Transaction transaction = db.Transactions.Single(t => t.Uid == transactionUid);
                Assert.Equal(cp.ChargePointId, transaction.ChargePointId);
                Assert.Equal(1, transaction.ConnectorId);
                Assert.Equal(tagId, transaction.StartTagId);
                Assert.Equal(1.0, transaction.MeterStart);
                Assert.Equal(12.5, transaction.MeterStop);
                Assert.Equal(tagId, transaction.StopTagId);
                Assert.NotNull(transaction.StopTime);

                ConnectorStatus? status = db.ConnectorStatuses.Find(cp.ChargePointId, 1);
                Assert.NotNull(status);
                Assert.Equal(12.5, status.LastMeter);
            }
        }

        [Fact]
        public async Task TransactionEvent_UpdatedForUnknownTransaction_ReturnsCallError()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Updated",
                timestamp = DateTimeOffset.UtcNow,
                triggerReason = "MeterValuePeriodic",
                seqNo = 5,
                transactionInfo = new { transactionId = Guid.NewGuid().ToString() },
                evse = new { id = 1, connectorId = 1 },
                // without meter value the server runs into a NullReferenceException and answers "Accepted" (see finding #44)
                meterValue = EnergyMeterValue(DateTimeOffset.UtcNow, 1000)
            });

            // current behavior (see finding #08)
            Assert.True(result.IsError);
            Assert.Equal("PropertyConstraintViolation", result.ErrorCode);
        }

        [Fact]
        public async Task MeterValues_UpdatesConnectorMeter()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("MeterValues", new { evseId = 1, meterValue = EnergyMeterValue(DateTimeOffset.UtcNow, 7500) });

            Assert.False(result.IsError);
            using OCPPCoreContext db = _factory.CreateDbContext();
            ConnectorStatus? status = db.ConnectorStatuses.Find(cp.ChargePointId, 1);
            Assert.NotNull(status);
            Assert.Equal(7.5, status.LastMeter);
        }

        [Fact]
        public async Task DataTransfer_IsAnswered()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("DataTransfer", new { vendorId = "Test", messageId = "Ping" });

            Assert.False(result.IsError);
        }

        [Fact]
        public async Task UnknownAction_ReturnsCallError()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("FooBar", new { });

            Assert.True(result.IsError);
            Assert.Equal("NotSupported", result.ErrorCode);
        }
    }
}
