using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// The expiry date of a charge tag is a calendar date (local time) and means "valid until the end of this day"
    /// </summary>
    public class TagExpiryTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TagExpiryTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private async Task<string?> AuthorizeStatusAsync(string protocol, string tagId)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            if (protocol == OcppProtocols.Ocpp16)
            {
                OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });
                return result.Payload?.SelectToken("idTagInfo.status")?.Value<string>();
            }
            OcppResult result2x = await cp.CallAsync("Authorize", new { idToken = new { idToken = tagId, type = "ISO14443" } });
            return result2x.Payload?.SelectToken("idTokenInfo.status")?.Value<string>();
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task ExpiryDateToday_IsStillValid(string protocol)
        {
            string tagId = _factory.AddChargeTag(expiryDate: DateTime.Today);

            Assert.Equal("Accepted", await AuthorizeStatusAsync(protocol, tagId));
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task ExpiryDateYesterday_IsExpired(string protocol)
        {
            string tagId = _factory.AddChargeTag(expiryDate: DateTime.Today.AddDays(-1));

            Assert.Equal("Expired", await AuthorizeStatusAsync(protocol, tagId));
        }

        [Fact]
        public async Task Ocpp16_ExpiryDateSentToChargePoint_IsEndOfTheDay()
        {
            DateTime expiryDate = DateTime.Today.AddDays(10);
            string tagId = _factory.AddChargeTag(expiryDate: expiryDate);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });

            // 0:00 (local time) of the following day
            DateTimeOffset sent = DateTimeOffset.Parse(result.Payload!.SelectToken("idTagInfo.expiryDate")!.ToString(Newtonsoft.Json.Formatting.None).Trim('"'));
            Assert.Equal(new DateTimeOffset(expiryDate.AddDays(1)), sent);
        }
    }
}
