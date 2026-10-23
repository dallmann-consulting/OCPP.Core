using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace OCPP.Core.Server.Tests.Infrastructure
{
    /// <summary>
    /// The TestServer has no remote IP address. This filter sets one before the OCPP middleware runs:
    /// default is loopback (= caller on the same computer, like the management UI),
    /// the header <see cref="HeaderName"/> simulates a caller from the network.
    /// </summary>
    public class RemoteIpStartupFilter : IStartupFilter
    {
        public const string HeaderName = "X-Test-RemoteIp";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    string? remoteIp = context.Request.Headers[HeaderName].FirstOrDefault();
                    context.Connection.RemoteIpAddress = string.IsNullOrEmpty(remoteIp) ? IPAddress.Loopback : IPAddress.Parse(remoteIp);
                    await nextMiddleware();
                });
                next(app);
            };
        }
    }
}
