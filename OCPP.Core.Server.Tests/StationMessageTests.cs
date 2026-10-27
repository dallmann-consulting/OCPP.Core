using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// OCPP 2.x messages of the charge point without own processing
    /// (reports, not offered functions, unexpected answers) and unsupported messages => answer + EventLog
    /// </summary>
    public class StationMessageTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public StationMessageTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private static readonly string Now = DateTimeOffset.UtcNow.ToString("o");

        /// <summary>
        /// Minimal valid messages (required fields of the schemas)
        /// </summary>
        private static object Payload(string action, string protocol) => action switch
        {
            "SecurityEventNotification" => new { type = "TamperDetectionActivated", timestamp = Now, techInfo = "cover opened" },
            "SignCertificate" => new { csr = "-----BEGIN CERTIFICATE REQUEST-----", certificateType = "ChargingStationCertificate" },
            "Get15118EVCertificate" => new { iso15118SchemaVersion = "urn:iso:15118:2:2013:MsgDef", action = "Install", exiRequest = "AAAA" },
            "GetCertificateStatus" => new { ocspRequestData = new { hashAlgorithm = "SHA256", issuerNameHash = "a", issuerKeyHash = "b", serialNumber = "c", responderURL = "http://ocsp" } },
            "NotifyEVChargingNeeds" => new { evseId = 1, chargingNeeds = new { requestedEnergyTransfer = "AC_single_phase" } },
            "ReservationStatusUpdate" => new { reservationId = 1, reservationUpdateStatus = "Expired" },
            "NotifyMonitoringReport" => new { requestId = 1, seqNo = 0, generatedAt = Now },
            "ReportChargingProfiles" => new
            {
                requestId = 1,
                chargingLimitSource = "EMS",
                evseId = 1,
                chargingProfile = new[]
                {
                    new
                    {
                        id = 1, stackLevel = 0, chargingProfilePurpose = "TxDefaultProfile", chargingProfileKind = "Absolute",
                        chargingSchedule = new[] { new { id = 1, chargingRateUnit = "W", chargingSchedulePeriod = new[] { new { startPeriod = 0, limit = 11000.0 } } } }
                    }
                }
            },
            "NotifyCustomerInformation" => new { data = "x", seqNo = 0, generatedAt = Now, requestId = 1 },
            "NotifyDisplayMessages" => new { requestId = 1 },
            "PublishFirmwareStatusNotification" => new { status = "Idle" },
            _ => throw new ArgumentException(action)
        };

        public static TheoryData<string, string, string, string?> Messages()
        {
            // action, expected status in the answer (null = empty answer), expected event code
            (string action, string? status, string eventCode)[] messages =
            {
                ("SecurityEventNotification", null, EventCode.SecurityEvent),
                ("SignCertificate", "Rejected", EventCode.FunctionNotSupported),
                ("Get15118EVCertificate", "Failed", EventCode.FunctionNotSupported),
                ("GetCertificateStatus", "Failed", EventCode.FunctionNotSupported),
                ("NotifyEVChargingNeeds", "Rejected", EventCode.FunctionNotSupported),
                ("ReservationStatusUpdate", null, EventCode.UnexpectedMessage),
                ("NotifyMonitoringReport", null, EventCode.UnexpectedMessage),
                ("ReportChargingProfiles", null, EventCode.UnexpectedMessage),
                ("NotifyCustomerInformation", null, EventCode.UnexpectedMessage),
                ("NotifyDisplayMessages", null, EventCode.UnexpectedMessage),
                ("PublishFirmwareStatusNotification", null, EventCode.UnexpectedMessage),
            };
            TheoryData<string, string, string, string?> data = new TheoryData<string, string, string, string?>();
            foreach (string protocol in new[] { OcppProtocols.Ocpp201, OcppProtocols.Ocpp21 })
            {
                foreach ((string action, string? status, string eventCode) in messages)
                {
                    // OCPP 2.1 has its own status for "no charging schedule"
                    string? expected = (action == "NotifyEVChargingNeeds" && protocol == OcppProtocols.Ocpp21) ? "NoChargingProfile" : status;
                    data.Add(protocol, action, eventCode, expected);
                }
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(Messages))]
        public async Task Message_IsAnsweredAndLogged(string protocol, string action, string eventCode, string? expectedStatus)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            OcppResult result = await cp.CallAsync(action, Payload(action, protocol));

            Assert.Equal(3, result.MessageType);
            Assert.Equal(expectedStatus, result.Payload?["status"]?.Value<string>());
            AssertValidAnswer(protocol, action, result.Payload!);

            ChargePointEvent e = Assert.Single(Events(chargePointId));
            Assert.Equal(eventCode, e.EventCode);
            Assert.Equal(EventCategory.Station, e.Category);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task SecurityEvent_SeverityAndParameters(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            await cp.CallAsync("SecurityEventNotification", new { type = "StartupOfTheDevice", timestamp = Now });
            await cp.CallAsync("SecurityEventNotification", new { type = "InvalidCsmsCertificate", timestamp = Now, techInfo = "expired" });
            await cp.CallAsync("SecurityEventNotification", new { type = "TamperDetectionActivated", timestamp = Now });

            List<ChargePointEvent> events = Events(chargePointId);
            Assert.Equal(new[] { EventSeverity.Info, EventSeverity.Warning, EventSeverity.Error }, events.Select(e => e.Severity));
            Assert.Equal("InvalidCsmsCertificate", Parameters(events[1])["type"]);
            Assert.Equal("expired", Parameters(events[1])["techInfo"]);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task NotifyEvent_OneEventPerEntry_PeriodicIgnored(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            OcppResult result = await cp.CallAsync("NotifyEvent", new
            {
                generatedAt = Now,
                seqNo = 0,
                eventData = new object[]
                {
                    // problem of a connector
                    new { eventId = 1, timestamp = Now, trigger = "Delta", actualValue = "true", eventNotificationType = "HardWiredNotification", techCode = "E42",
                          component = new { name = "ConnectorPlugRetentionLock", evse = new { id = 1, connectorId = 1 } }, variable = new { name = "Problem" }, techInfo = "lock failure" },
                    // threshold monitor
                    new { eventId = 2, timestamp = Now, trigger = "Alerting", actualValue = "75", eventNotificationType = "CustomMonitor",
                          component = new { name = "ChargingStation" }, variable = new { name = "Temperature" } },
                    // periodic value => no event
                    new { eventId = 3, timestamp = Now, trigger = "Periodic", actualValue = "230", eventNotificationType = "CustomMonitor",
                          component = new { name = "EVSE", evse = new { id = 1 } }, variable = new { name = "Voltage" } },
                    // problem solved
                    new { eventId = 4, timestamp = Now, trigger = "Delta", actualValue = "false", eventNotificationType = "HardWiredNotification", cleared = true,
                          component = new { name = "ConnectorPlugRetentionLock", evse = new { id = 1, connectorId = 1 } }, variable = new { name = "Problem" } }
                }
            });
            Assert.Equal(3, result.MessageType);

            List<ChargePointEvent> events = Events(chargePointId);
            Assert.Equal(3, events.Count);
            Assert.All(events, e => Assert.Equal(EventCode.StationEvent, e.EventCode));

            Assert.Equal(EventSeverity.Error, events[0].Severity);
            Assert.Equal(1, events[0].ConnectorId);
            Assert.Equal("ConnectorPlugRetentionLock", Parameters(events[0])["component"]);
            Assert.Equal("Problem", Parameters(events[0])["variable"]);
            Assert.Equal("E42", Parameters(events[0])["techCode"]);
            Assert.Equal("lock failure", events[0].Details);

            Assert.Equal(EventSeverity.Warning, events[1].Severity);
            Assert.Null(events[1].ConnectorId);

            Assert.Equal(EventSeverity.Info, events[2].Severity);
            Assert.Equal("true", Parameters(events[2])["cleared"]);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201, "FormatViolation")]
        [InlineData(OcppProtocols.Ocpp21, "FormatViolation")]
        public async Task InvalidMessage_IsFormatViolation(string protocol, string errorCode)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            // "type" is missing
            OcppResult result = await cp.CallAsync("SecurityEventNotification", new { timestamp = Now });

            Assert.True(result.IsError);
            Assert.Equal(errorCode, result.ErrorCode);
            Assert.Empty(Events(chargePointId));
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp16, "FirmwareStatusNotification")]
        [InlineData(OcppProtocols.Ocpp16, "SecurityEventNotification")]
        [InlineData(OcppProtocols.Ocpp201, "FooBar")]
        [InlineData(OcppProtocols.Ocpp21, "BatterySwap")]
        public async Task UnsupportedMessage_IsRejectedAndLogged(string protocol, string action)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            OcppResult result = await cp.CallAsync(action, new { status = "Idle" });

            Assert.True(result.IsError);
            Assert.Equal("NotSupported", result.ErrorCode);
            ChargePointEvent e = Assert.Single(Events(chargePointId));
            Assert.Equal(EventCode.UnsupportedMessage, e.EventCode);
            Assert.Equal(EventSeverity.Warning, e.Severity);
            Assert.Equal(action, Parameters(e)["action"]);
            Assert.Contains("Idle", e.Details);
        }

        private static void AssertValidAnswer(string protocol, string action, JObject payload)
        {
            string version = protocol == OcppProtocols.Ocpp21 ? "21" : "20";
            JsonSchema schema = JsonSchema.FromFileAsync(RepositoryPaths.File("OCPP.Core.Server", $"Schema{version}", $"{action}Response.json")).GetAwaiter().GetResult();
            Assert.Empty(schema.Validate(payload.ToString()));
        }

        private List<ChargePointEvent> Events(string chargePointId)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            // only the events of the messages (not "Connected")
            return db.ChargePointEvents.AsNoTracking()
                .Where(e => e.ChargePointId == chargePointId && e.Category == EventCategory.Station)
                .OrderBy(e => e.EventId)
                .ToList();
        }

        private static Dictionary<string, string> Parameters(ChargePointEvent e) => EventLogExtensions.ReadParameters(e.Parameters);
    }
}
