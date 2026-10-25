using System.Net;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Concurrency bug: the status API serializes the online connectors while the charge point changes them
    /// (before: "Collection was modified" => HTTP 500 without content)
    /// </summary>
    public class StatusApiConcurrencyTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public StatusApiConcurrencyTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Status_WhileConnectorsChange_IsAlwaysValid()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            HttpClient client = _factory.CreateClient();
            using CancellationTokenSource stop = new CancellationTokenSource();

            // the charge point reports new connectors (=> new entries in the dictionary)
            Task changes = Task.Run(async () =>
            {
                for (int connectorId = 1; connectorId <= 300; connectorId++)
                {
                    await cp.CallAsync("StatusNotification", new { connectorId, errorCode = "NoError", status = "Available", timestamp = DateTimeOffset.UtcNow });
                }
                stop.Cancel();
            }, TestContext.Current.CancellationToken);

            int requests = 0;
            List<string> errors = new List<string>();
            while (!stop.IsCancellationRequested)
            {
                HttpResponseMessage response = await client.GetAsync("/API/Status", TestContext.Current.CancellationToken);
                string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                requests++;
                if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrEmpty(content))
                {
                    errors.Add($"{(int)response.StatusCode}: '{content}'");
                    continue;
                }
                JArray.Parse(content);
            }
            await changes;

            Assert.True(errors.Count == 0, $"{errors.Count} of {requests} status requests failed: {string.Join(" | ", errors.Take(3))}");
        }
    }
}
