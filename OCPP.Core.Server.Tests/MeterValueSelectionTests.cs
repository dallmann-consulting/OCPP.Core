using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Selection of meter value, charging power and SoC from the sampled values (phase, location, unit, timestamp)
    /// </summary>
    public class MeterValueEvaluatorTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        private static MeterSample Energy(double value, string? phase = null, string? location = null, string? unit = "Wh", int multiplier = 0, DateTimeOffset? timestamp = null)
            => new MeterSample(timestamp ?? T0, "Energy.Active.Import.Register", phase!, location!, value, unit!, multiplier);

        private static MeterSample Power(double value, string? phase = null, string? location = null, string? unit = "W", DateTimeOffset? timestamp = null)
            => new MeterSample(timestamp ?? T0, "Power.Active.Import", phase!, location!, value, unit!);

        private static MeterReading Evaluate(params MeterSample[] samples) => MeterValueEvaluator.Evaluate(samples, NullLogger.Instance);

        [Fact]
        public void SingleValues_AreConverted()
        {
            MeterReading r = Evaluate(Energy(12345), Power(7400), new MeterSample(T0, "SoC", null!, "EV", 80, "Percent"));

            Assert.Equal(12.345, r.MeterKWH, 6);
            Assert.Equal(7.4, r.CurrentChargeKW, 6);
            Assert.Equal(80, r.StateOfCharge);
            Assert.Equal(T0, r.MeterTime);
        }

        [Fact]
        public void WithoutMeasurand_IsEnergy()
        {
            Assert.Equal(1.5, Evaluate(new MeterSample(T0, null!, null!, null!, 1500, null!)).MeterKWH, 6);
        }

        [Fact]
        public void PowerPerPhase_IsSummed()
        {
            Assert.Equal(11.1, Evaluate(Power(3700, "L1"), Power(3700, "L2"), Power(3700, "L3")).CurrentChargeKW, 6);
            Assert.Equal(11.1, Evaluate(Power(3700, "L1-N"), Power(3700, "L2-N"), Power(3700, "L3-N")).CurrentChargeKW, 6);
            // single-phase charging
            Assert.Equal(3.7, Evaluate(Power(3700, "L1")).CurrentChargeKW, 6);
        }

        [Fact]
        public void PowerNeutralAndLineToLine_AreIgnored()
        {
            Assert.Equal(7.4, Evaluate(Power(3700, "L1"), Power(3700, "L2"), Power(100, "N"), Power(6400, "L1-L2")).CurrentChargeKW, 6);
        }

        [Fact]
        public void TotalValue_IsPreferredToPhases()
        {
            MeterReading r = Evaluate(Power(3700, "L1"), Power(11000), Power(3700, "L3"), Energy(1000, "L1"), Energy(3000), Energy(1000, "L2"));

            Assert.Equal(11.0, r.CurrentChargeKW, 6);
            Assert.Equal(3.0, r.MeterKWH, 6);
        }

        [Fact]
        public void EnergyPerPhase_SummedOnlyWhenComplete()
        {
            Assert.Equal(6.0, Evaluate(Energy(1000, "L1"), Energy(2000, "L2-N"), Energy(3000, "L3")).MeterKWH, 6);
            // L3 missing => no total
            Assert.Equal(-1, Evaluate(Energy(1000, "L1"), Energy(2000, "L2")).MeterKWH);
        }

        [Fact]
        public void EvBodyCable_AreIgnored()
        {
            Assert.Equal(10.0, Evaluate(Energy(50000, location: "EV"), Energy(10000, location: "Outlet"), Energy(20000, location: "Cable")).MeterKWH, 6);
            Assert.Equal(-1, Evaluate(Energy(50000, location: "EV"), Energy(1, location: "Body")).MeterKWH);
            Assert.Equal(-1, Evaluate(Power(7000, location: "EV")).CurrentChargeKW);
        }

        [Fact]
        public void Inlet_OnlyWithoutOutletValue()
        {
            Assert.Equal(20.0, Evaluate(Energy(20000, location: "Inlet")).MeterKWH, 6);
            Assert.Equal(10.0, Evaluate(Energy(20000, location: "Inlet"), Energy(10000)).MeterKWH, 6);
        }

        [Fact]
        public void UnitsAndMultiplier()
        {
            Assert.Equal(5.0, Evaluate(Energy(5, unit: "Wh", multiplier: 3)).MeterKWH, 6);
            Assert.Equal(5.0, Evaluate(Energy(50, unit: "kWh", multiplier: -1)).MeterKWH, 6);
            Assert.Equal(5.0, Evaluate(Energy(5, unit: "KWH")).MeterKWH, 6);
            Assert.Equal(7.4, Evaluate(Power(7.4, unit: "kW")).CurrentChargeKW, 6);
            Assert.Equal(7.4, Evaluate(Power(7.4, unit: "KW")).CurrentChargeKW, 6);
            // without unit: Wh / W
            Assert.Equal(1.0, Evaluate(Energy(1000, unit: null)).MeterKWH, 6);
        }

        [Fact]
        public void UnknownUnit_IsIgnored()
        {
            Assert.Equal(-1, Evaluate(Energy(16, unit: "A")).MeterKWH);
            Assert.Equal(2.0, Evaluate(Energy(16, unit: "A"), Energy(2000)).MeterKWH, 6);
        }

        [Fact]
        public void NewestTimestamp_Wins()
        {
            MeterReading r = Evaluate(Energy(3000, timestamp: T0.AddMinutes(10)), Energy(1000, timestamp: T0), Energy(2000, timestamp: T0.AddMinutes(5)));

            Assert.Equal(3.0, r.MeterKWH, 6);
            Assert.Equal(T0.AddMinutes(10), r.MeterTime);
        }

        [Fact]
        public void NewestTimestampWithoutUsableValue_OlderValueIsUsed()
        {
            // newest only from the EV => the older outlet value is used
            MeterReading r = Evaluate(Energy(1000, timestamp: T0), Energy(9000, location: "EV", timestamp: T0.AddMinutes(1)));

            Assert.Equal(1.0, r.MeterKWH, 6);
            Assert.Equal(T0, r.MeterTime);
        }
    }

    /// <summary>
    /// End-to-end: meter value messages of all OCPP versions
    /// </summary>
    public class MeterValueSelectionTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;
        private readonly HttpClient _client;

        public MeterValueSelectionTests(OcppServerFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        // three-phase charging: power per phase, energy at the outlet and of the EV
        private static object[] ThreePhaseSamples(DateTimeOffset timestamp, double outletWh) => new object[]
        {
            new { timestamp, sampledValue = new object[]
            {
                new { value = outletWh, measurand = "Energy.Active.Import.Register", location = "Outlet", unitOfMeasure = new { unit = "Wh" } },
                new { value = 99000, measurand = "Energy.Active.Import.Register", location = "EV", unitOfMeasure = new { unit = "Wh" } },
                new { value = 3680, measurand = "Power.Active.Import", phase = "L1-N", unitOfMeasure = new { unit = "W" } },
                new { value = 3680, measurand = "Power.Active.Import", phase = "L2-N", unitOfMeasure = new { unit = "W" } },
                new { value = 3680, measurand = "Power.Active.Import", phase = "L3-N", unitOfMeasure = new { unit = "W" } },
                new { value = 55, measurand = "SoC", location = "EV", unitOfMeasure = new { unit = "Percent" } }
            } }
        };

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_TransactionEvent_UsesOutletAndSumsPhases(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, ThreePhaseSamples(start, 10000));
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(10), ThreePhaseSamples(start.AddMinutes(10), 12000));

            Transaction transaction = LoadTransaction(uid);
            Assert.Equal(10.0, transaction.MeterStart, 6);
            Assert.Equal(12.0, transaction.MeterStop!.Value, 6);

            JToken connector = await ConnectorStatusAsync(cp.ChargePointId, 1);
            Assert.Equal(11.04, Value(connector, "ChargeRateKW"), 6);
            Assert.Equal(55, Value(connector, "SoC"));

            // multiplier: 13 * 10^3 Wh = 13 kWh
            await TransactionEventAsync(cp, "Ended", uid, 2, start.AddMinutes(20), new object[]
            {
                new { timestamp = start.AddMinutes(20), sampledValue = new object[]
                {
                    new { value = 13, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh", multiplier = 3 } }
                } }
            });
            Assert.Equal(13.0, LoadTransaction(uid).MeterStop!.Value, 6);
        }

        [Fact]
        public async Task Ocpp16_MeterValues_UsesOutletAndSumsPhases()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            OcppResult result = await cp.CallAsync("MeterValues", new
            {
                connectorId = 1,
                meterValue = new object[]
                {
                    new { timestamp = now.AddMinutes(-1), sampledValue = new object[]
                    {
                        new { value = "4000", measurand = "Energy.Active.Import.Register", unit = "Wh" }
                    } },
                    new { timestamp = now, sampledValue = new object[]
                    {
                        new { value = "5000", measurand = "Energy.Active.Import.Register", unit = "Wh" },
                        new { value = "77000", measurand = "Energy.Active.Import.Register", location = "EV", unit = "Wh" },
                        new { value = "2300", measurand = "Power.Active.Import", phase = "L1", unit = "W" },
                        new { value = "2300", measurand = "Power.Active.Import", phase = "L2", unit = "W" },
                        new { value = "2300", measurand = "Power.Active.Import", phase = "L3", unit = "W" },
                        new { value = "230", measurand = "Voltage", phase = "L1-N", unit = "V" }
                    } }
                }
            });
            Assert.Equal(3, result.MessageType);

            JToken connector = await ConnectorStatusAsync(cp.ChargePointId, 1);
            Assert.Equal(5.0, Value(connector, "MeterKWH"), 6);
            Assert.Equal(6.9, Value(connector, "ChargeRateKW"), 6);

            using OCPPCoreContext db = _factory.CreateDbContext();
            Assert.Equal(5.0, db.ConnectorStatuses.Single(c => c.ChargePointId == cp.ChargePointId && c.ConnectorId == 1).LastMeter!.Value, 6);
        }

        private Transaction LoadTransaction(string uid)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.Single(t => t.Uid == uid);
        }

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, object[] meterValue)
        {
            return cp.CallAsync("TransactionEvent", new
            {
                eventType,
                timestamp,
                triggerReason = eventType == "Started" ? "CablePluggedIn" : "MeterValuePeriodic",
                seqNo,
                transactionInfo = new { transactionId = uid },
                evse = new { id = 1, connectorId = 1 },
                meterValue
            });
        }

        private async Task<JToken> ConnectorStatusAsync(string chargePointId, int connectorId)
        {
            HttpResponseMessage response = await _client.GetAsync("/API/Status", TestContext.Current.CancellationToken);
            JArray status = JArray.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            JToken entry = status.Single(s => s["id"]?.Value<string>() == chargePointId);
            JObject connectors = (JObject)Property(entry, "OnlineConnectors");
            return connectors.Properties().Single(p => p.Name == connectorId.ToString()).Value;
        }

        private static JToken Property(JToken token, string name)
            => ((JObject)token).Properties().Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;

        private static double Value(JToken token, string name) => Property(token, name).Value<double>();
    }
}
