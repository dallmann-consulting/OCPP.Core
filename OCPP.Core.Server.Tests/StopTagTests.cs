using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Who may stop a transaction?
    /// The charging station decides (same tag or same group via Authorize + parentIdTag/groupIdToken).
    /// The server always closes the transaction and answers with the real status of the stop tag.
    /// </summary>
    public class StopTagTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public StopTagTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        private Transaction LoadTransaction(Func<Transaction, bool> filter)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.AsEnumerable().Where(filter).OrderByDescending(t => t.TransactionId).First();
        }

        private static object IdToken(string tagId) => new { idToken = tagId, type = "ISO14443" };

        // ---------- group information in Authorize (basis for the decision of the charging station) ----------

        [Fact]
        public async Task Ocpp16_Authorize_TagWithoutGroup_HasNoParentIdTag()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });

            JObject idTagInfo = (JObject)result.Payload!["idTagInfo"]!;
            Assert.Equal("Accepted", idTagInfo["status"]!.Value<string>());
            // an empty "parentIdTag" could be interpreted as "same group" for all tags without group
            Assert.Null(idTagInfo["parentIdTag"]);
        }

        [Fact]
        public async Task Ocpp16_Authorize_UnknownTag_HasNoParentIdTag()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = "UNKNOWNTAG" });

            Assert.Null(result.Payload!["idTagInfo"]!["parentIdTag"]);
        }

        [Fact]
        public async Task Ocpp16_Authorize_TagWithGroup_HasParentIdTag()
        {
            string parentTag = _factory.AddChargeTag();
            string tagId = _factory.AddChargeTag(parentTagId: parentTag);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            OcppResult result = await cp.CallAsync("Authorize", new { idTag = tagId });

            Assert.Equal(parentTag, result.Payload!["idTagInfo"]!["parentIdTag"]!.Value<string>());
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_Authorize_GroupIdTokenOnlyForTagWithGroup(string protocol)
        {
            string parentTag = _factory.AddChargeTag();
            string tagWithGroup = _factory.AddChargeTag(parentTagId: parentTag);
            string tagWithoutGroup = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult withGroup = await cp.CallAsync("Authorize", new { idToken = IdToken(tagWithGroup) });
            OcppResult withoutGroup = await cp.CallAsync("Authorize", new { idToken = IdToken(tagWithoutGroup) });

            Assert.Equal(parentTag, withGroup.Payload!["idTokenInfo"]!["groupIdToken"]!["idToken"]!.Value<string>());
            Assert.Null(withoutGroup.Payload!["idTokenInfo"]!["groupIdToken"]);
        }

        // ---------- OCPP 1.6: stop with another tag ----------

        private async Task<(ChargePointSimulator Cp, int TransactionId)> StartOcpp16TransactionAsync(string startTagId)
        {
            ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            OcppResult start = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = startTagId, meterStart = 1000, timestamp = DateTimeOffset.UtcNow.AddMinutes(-30) });
            return (cp, start.Payload!["transactionId"]!.Value<int>());
        }

        [Theory]
        [InlineData("otherValidTag", "Accepted")]   // valid tag outside the group => real status, no artificial "Invalid"
        [InlineData("otherBlockedTag", "Blocked")]
        [InlineData("unknownTag", "Invalid")]
        [InlineData("sameGroupTag", "Accepted")]
        public async Task Ocpp16_StopWithOtherTag_IsClosedWithRealStatus(string stopTagKind, string expectedStatus)
        {
            string parentTag = _factory.AddChargeTag();
            string startTag = _factory.AddChargeTag(parentTagId: parentTag);
            string stopTag = stopTagKind switch
            {
                "otherValidTag" => _factory.AddChargeTag(),
                "otherBlockedTag" => _factory.AddChargeTag(blocked: true),
                "sameGroupTag" => _factory.AddChargeTag(parentTagId: parentTag),
                _ => "UNKNOWN" + Guid.NewGuid().ToString("N").Substring(0, 8)
            };
            (ChargePointSimulator cp, int transactionId) = await StartOcpp16TransactionAsync(startTag);
            await using (cp)
            {
                OcppResult stop = await cp.CallAsync("StopTransaction", new { transactionId, idTag = stopTag, meterStop = 5000, timestamp = DateTimeOffset.UtcNow, reason = "Local" });

                Assert.False(stop.IsError);
                Assert.Equal(expectedStatus, stop.Payload!["idTagInfo"]!["status"]!.Value<string>());
                Transaction transaction = LoadTransaction(t => t.TransactionId == transactionId);
                Assert.NotNull(transaction.StopTime);
                Assert.Equal(5.0, transaction.MeterStop);
                Assert.Equal(stopTag, transaction.StopTagId);
            }
        }

        [Fact]
        public async Task Ocpp16_StopWithStartTag_BlockedInTheMeantime_IsAccepted()
        {
            string startTag = _factory.AddChargeTag();
            (ChargePointSimulator cp, int transactionId) = await StartOcpp16TransactionAsync(startTag);
            await using (cp)
            {
                using (OCPPCoreContext db = _factory.CreateDbContext())
                {
                    db.ChargeTags.Find(startTag)!.Blocked = true;
                    db.SaveChanges();
                }

                OcppResult stop = await cp.CallAsync("StopTransaction", new { transactionId, idTag = startTag, meterStop = 5000, timestamp = DateTimeOffset.UtcNow, reason = "Local" });

                // the start tag can always stop its transaction (owner must be able to release the car)
                Assert.Equal("Accepted", stop.Payload!["idTagInfo"]!["status"]!.Value<string>());
                Assert.NotNull(LoadTransaction(t => t.TransactionId == transactionId).StopTime);
            }
        }

        // ---------- OCPP 2.x: ended with another token ----------

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedWithOtherToken_IsClosedWithRealStatus(string protocol)
        {
            string startTag = _factory.AddChargeTag();
            string otherTag = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Started", timestamp = start, triggerReason = "Authorized", seqNo = 0,
                transactionInfo = new { transactionId = uid }, idToken = IdToken(startTag), evse = new { id = 1, connectorId = 1 }
            });
            OcppResult ended = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Ended", timestamp = start.AddMinutes(30), triggerReason = "StopAuthorized", seqNo = 1,
                transactionInfo = new { transactionId = uid, stoppedReason = "Local" }, idToken = IdToken(otherTag), evse = new { id = 1, connectorId = 1 }
            });

            Assert.False(ended.IsError);
            Assert.Equal("Accepted", ended.Payload!["idTokenInfo"]!["status"]!.Value<string>());
            Transaction transaction = LoadTransaction(t => t.Uid == uid);
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(otherTag, transaction.StopTagId);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedWithUnknownToken_IsClosed(string protocol)
        {
            string startTag = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Started", timestamp = start, triggerReason = "Authorized", seqNo = 0,
                transactionInfo = new { transactionId = uid }, idToken = IdToken(startTag), evse = new { id = 1, connectorId = 1 }
            });
            OcppResult ended = await cp.CallAsync("TransactionEvent", new
            {
                eventType = "Ended", timestamp = start.AddMinutes(30), triggerReason = "StopAuthorized", seqNo = 1,
                transactionInfo = new { transactionId = uid, stoppedReason = "Local" }, idToken = IdToken("UNKNOWNTOKEN"), evse = new { id = 1, connectorId = 1 }
            });

            Assert.False(ended.IsError);
            Assert.NotEqual("Accepted", ended.Payload!["idTokenInfo"]!["status"]!.Value<string>());
            Assert.NotNull(LoadTransaction(t => t.Uid == uid).StopTime);
        }
    }
}
