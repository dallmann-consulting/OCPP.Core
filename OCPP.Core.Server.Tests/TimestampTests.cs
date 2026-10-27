using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Times of the charge point with an offset are stored as UTC (as all times in the database)
    /// </summary>
    public class TimestampTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TimestampTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private static readonly DateTimeOffset LocalTime = new DateTimeOffset(2026, 10, 27, 14, 30, 0, TimeSpan.FromHours(2));
        private static readonly DateTime ExpectedUtc = new DateTime(2026, 10, 27, 12, 30, 0);

        private ConnectorStatus LoadConnector(string chargePointId)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.ConnectorStatuses.AsNoTracking().Single(c => c.ChargePointId == chargePointId && c.ConnectorId == 1);
        }

        [Fact]
        public async Task Ocpp16_StatusAndMeterTimeWithOffset_AreStoredAsUtc()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            await cp.CallAsync("StatusNotification", new { connectorId = 1, errorCode = "NoError", status = "Available", timestamp = LocalTime });
            await cp.CallAsync("MeterValues", new
            {
                connectorId = 1,
                meterValue = new[] { new { timestamp = LocalTime, sampledValue = new[] { new { value = "1000", measurand = "Energy.Active.Import.Register", unit = "Wh" } } } }
            });

            ConnectorStatus status = LoadConnector(chargePointId);
            Assert.Equal(ExpectedUtc, status.LastStatusTime);
            Assert.Equal(ExpectedUtc, status.LastMeterTime);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task Ocpp2x_StatusTimeWithOffset_IsStoredAsUtc(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            await cp.CallAsync("StatusNotification", new { timestamp = LocalTime, connectorStatus = "Available", evseId = 1, connectorId = 1 });

            Assert.Equal(ExpectedUtc, LoadConnector(chargePointId).LastStatusTime);
        }
    }
}
