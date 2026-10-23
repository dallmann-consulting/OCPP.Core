using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Messages from an OCPP 1.6 charge point to the server
    /// </summary>
    public class Ocpp16Tests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public Ocpp16Tests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private async Task<ChargePointSimulator> ConnectNewChargePointAsync()
        {
            return await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
        }

        [Fact]
        public async Task BootNotification_IsAccepted()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("BootNotification", new { chargePointVendor = "Test", chargePointModel = "Simulator" });

            Assert.False(result.IsError);
            Assert.Equal("Accepted", result.Payload!["status"]!.Value<string>());
            Assert.Equal(300, result.Payload["interval"]!.Value<int>());
            Assert.NotNull(result.Payload["currentTime"]);
        }

        [Fact]
        public async Task Heartbeat_ReturnsCurrentTime()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Heartbeat", new { });

            Assert.False(result.IsError);
            DateTimeOffset currentTime = result.Payload!["currentTime"]!.Value<DateTime>();
            Assert.True((DateTimeOffset.UtcNow - currentTime).Duration() < TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task UnknownAction_ReturnsCallError()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("FooBar", new { });

            Assert.True(result.IsError);
            Assert.Equal("NotSupported", result.ErrorCode);
        }

        [Fact]
        public async Task StatusNotification_UpdatesConnectorStatus()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("StatusNotification", new { connectorId = 1, errorCode = "NoError", status = "Charging", timestamp = DateTimeOffset.UtcNow });

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

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });

            Assert.Equal("Accepted", result.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Authorize_UnknownTag_IsInvalid()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = "UNKNOWN" + Guid.NewGuid().ToString("N").Substring(0, 8) });

            Assert.Equal("Invalid", result.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Authorize_BlockedTag_IsBlocked()
        {
            string tagId = _factory.AddChargeTag(blocked: true);
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });

            Assert.Equal("Blocked", result.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Authorize_ExpiredTag_IsExpired()
        {
            string tagId = _factory.AddChargeTag(expiryDate: DateTime.Now.AddDays(-1));
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });

            Assert.Equal("Expired", result.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task Transaction_StartMeterStop_IsStoredInDatabase()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            OcppResult startResult = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });

            Assert.False(startResult.IsError);
            Assert.Equal("Accepted", startResult.Payload!["idTagInfo"]!["status"]!.Value<string>());
            int transactionId = startResult.Payload["transactionId"]!.Value<int>();
            Assert.True(transactionId > 0);

            OcppResult meterResult = await cp.CallAsync("MeterValues", new
            {
                connectorId = 1,
                transactionId,
                meterValue = new[]
                {
                    new
                    {
                        timestamp = start.AddMinutes(10),
                        sampledValue = new[]
                        {
                            new { value = "4000", measurand = "Energy.Active.Import.Register", unit = "Wh" },
                            new { value = "11000", measurand = "Power.Active.Import", unit = "W" }
                        }
                    }
                }
            });
            Assert.False(meterResult.IsError);

            OcppResult stopResult = await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 12500, timestamp = start.AddMinutes(30), reason = "Local" });

            Assert.False(stopResult.IsError);
            Assert.Equal("Accepted", stopResult.Payload!["idTagInfo"]!["status"]!.Value<string>());

            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction? transaction = db.Transactions.Find(transactionId);
            Assert.NotNull(transaction);
            Assert.Equal(cp.ChargePointId, transaction.ChargePointId);
            Assert.Equal(1, transaction.ConnectorId);
            Assert.Equal(tagId, transaction.StartTagId);
            Assert.Equal(1.0, transaction.MeterStart);
            Assert.Equal(12.5, transaction.MeterStop);
            Assert.Equal(tagId, transaction.StopTagId);
            Assert.Equal("Local", transaction.StopReason);
            Assert.NotNull(transaction.StopTime);

            ConnectorStatus? status = db.ConnectorStatuses.Find(cp.ChargePointId, 1);
            Assert.NotNull(status);
            Assert.Equal(12.5, status.LastMeter);
        }

        /// <summary>
        /// The transaction has already started at the charge point (e.g. authorized offline)
        /// => it is recorded with the authorization result
        /// </summary>
        [Fact]
        public async Task StartTransaction_UnknownTag_IsRecordedWithResult()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = "UNKNOWN1", meterStart = 0, timestamp = DateTimeOffset.UtcNow });

            Assert.False(result.IsError);
            Assert.Equal("Invalid", result.Payload!["idTagInfo"]!["status"]!.Value<string>());
            int transactionId = result.Payload["transactionId"]!.Value<int>();
            Assert.True(transactionId > 0);
            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction? transaction = db.Transactions.Find(transactionId);
            Assert.NotNull(transaction);
            Assert.Equal("UNKNOWN1", transaction.StartTagId);
            Assert.Equal("Invalid", transaction.StartResult);
        }

        [Fact]
        public async Task StopTransaction_UnknownTransaction_ReturnsCallError()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("StopTransaction", new { transactionId = 999999, meterStop = 100, timestamp = DateTimeOffset.UtcNow });

            // current behavior (see finding #08)
            Assert.True(result.IsError);
            Assert.Equal("PropertyConstraintViolation", result.ErrorCode);
        }

        [Fact]
        public async Task DataTransfer_IsAccepted()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            OcppResult result = await cp.CallAsync("DataTransfer", new { vendorId = "Test", messageId = "Ping", data = "123" });

            Assert.Equal("Accepted", result.Payload!["status"]!.Value<string>());
        }

        [Fact]
        public async Task InvalidPayload_IsRejectedBySchemaValidation()
        {
            await using ChargePointSimulator cp = await ConnectNewChargePointAsync();

            // "connectorId" is missing
            OcppResult result = await cp.CallAsync("StartTransaction", new { idTag = "X", meterStart = 0, timestamp = DateTimeOffset.UtcNow });

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }
    }
}
