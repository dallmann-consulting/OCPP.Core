namespace OCPP.Core.Server.Tests.Infrastructure
{
    /// <summary>
    /// WebSocket sub protocols supported by the server
    /// </summary>
    public static class OcppProtocols
    {
        public const string Ocpp16 = "ocpp1.6";
        public const string Ocpp201 = "ocpp2.0.1";
        public const string Ocpp21 = "ocpp2.1";

        /// <summary>
        /// All protocols (for [Theory] tests)
        /// </summary>
        public static TheoryData<string> All => new TheoryData<string>(Ocpp16, Ocpp201, Ocpp21);
    }

    /// <summary>
    /// Helper for conditions that are fulfilled asynchronously by the server
    /// </summary>
    public static class Eventually
    {
        public static async Task<bool> TrueAsync(Func<bool> condition, TimeSpan? timeout = null)
        {
            DateTime end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                await Task.Delay(20);
            }
            return condition();
        }
    }
}
