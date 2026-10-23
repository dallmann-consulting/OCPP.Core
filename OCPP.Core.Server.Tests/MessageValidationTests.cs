using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// JSON schema validation of incoming messages ("ValidateMessages": true).
    /// The invalid messages are chosen so that only the schema validation detects them
    /// (deserialization alone would accept them).
    /// </summary>
    public class MessageValidationTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public MessageValidationTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private static object AuthorizePayload(string protocol, object idTag) => (protocol == OcppProtocols.Ocpp16)
            ? new { idTag }
            : new { idToken = new { idToken = idTag, type = "ISO14443" } };

        /// <summary>
        /// Newtonsoft.Json.Schema without license: max. 1000 validations per hour
        /// => replaced with NJSONSchema.
        /// The validation must not stop working after a number of messages
        /// </summary>
        [Fact]
        public async Task ManyMessages_AreAllValidatedAndAccepted()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            // Heartbeat is not validated (no deserialization) => use Authorize
            for (int i = 0; i < 1100; i++)
            {
                OcppResult result = await cp.CallAsync("Authorize", new { idTag = "VALIDTAG" });
                Assert.False(result.IsError, $"Message {i + 1} was rejected: {result.ErrorCode}");
            }
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task ValidMessage_IsAccepted(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult result = await cp.CallAsync("Authorize", AuthorizePayload(protocol, "VALIDTAG"));

            Assert.False(result.IsError);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task AdditionalProperty_IsRejected(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            // all schemas have "additionalProperties": false
            OcppResult result = (protocol == OcppProtocols.Ocpp16)
                ? await cp.CallAsync("Authorize", new { idTag = "VALIDTAG", foo = 1 })
                : await cp.CallAsync("Authorize", new { idToken = new { idToken = "VALIDTAG", type = "ISO14443" }, foo = 1 });

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task WrongPropertyType_IsRejected(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            // number instead of string (deserialization would convert it silently)
            OcppResult result = await cp.CallAsync("Authorize", AuthorizePayload(protocol, 12345));

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task TooLongString_IsRejected(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            // 1.6: CiString20Type / 2.0.1: max. 36 / 2.1: max. 255
            OcppResult result = await cp.CallAsync("Authorize", AuthorizePayload(protocol, new string('A', 300)));

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }

        /// <summary>
        /// Only OCPP 2.0.1: in OCPP 2.1 the idToken type is a free string (max. 20 characters)
        /// </summary>
        [Fact]
        public async Task InvalidEnumValue_IsRejected()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp201);

            OcppResult result = await cp.CallAsync("Authorize", new { idToken = new { idToken = "VALIDTAG", type = "Sleeping" } });

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task MissingRequiredProperty_IsRejected(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult result = await cp.CallAsync("Authorize", new { });

            Assert.True(result.IsError);
            Assert.Equal("FormationViolation", result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task InvalidDateTime_IsRejected(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult result = (protocol == OcppProtocols.Ocpp16)
                ? await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = "VALIDTAG", meterStart = 0, timestamp = "yesterday" })
                : await cp.CallAsync("TransactionEvent", new
                {
                    eventType = "Started",
                    timestamp = "yesterday",
                    triggerReason = "Authorized",
                    seqNo = 0,
                    transactionInfo = new { transactionId = Guid.NewGuid().ToString() }
                });

            Assert.True(result.IsError);
        }
    }
}
