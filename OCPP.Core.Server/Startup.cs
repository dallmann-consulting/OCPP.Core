/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    public class Startup
    {
        /// <summary>
        /// ILogger object
        /// </summary>
        private ILoggerFactory LoggerFactory { get; set; }

        public Startup(IConfiguration configuration)
        {
            if (!configuration.GetSection("ConnectionStrings").Exists())
            {
                // Running the exe (Kestrel) hasn't loaded the config at this point!?
                // => Workaround: use the created configuration from main()
                configuration = Program._configuration;
            }            

            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOCPPDbContext(Configuration);
            services.AddControllers();
            services.AddHostedService<LogCleanupService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        //public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        public void Configure(IApplicationBuilder app,
                            IWebHostEnvironment env,
                            ILoggerFactory loggerFactory,
                            IServiceScopeFactory serviceScopeFactory)
        {
            LoggerFactory = loggerFactory;
            ILogger logger = loggerFactory.CreateLogger(typeof(Startup));
            logger.LogTrace("Startup => Configure(...)");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Migrate database
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();
            // but only when not disabled (needs admin permissions in SQL-Server!)
            bool dbMigrate = Configuration.GetValue<bool>("AutoMigrateDB", true);
            if (dbMigrate)
            {
                dbContext.EnsureMigrated(logger);
            }
            // warning if the database schema is older than the application (e.g. no rights for automatic migrations)
            dbContext.LogPendingMigrations(logger);

            LogSecurityWarnings(logger, dbContext);

            // Set WebSocketsOptions
            // Ping/Pong: the server sends a ping every "WebSocketPingInterval" seconds (0 = off)
            // and aborts the connection if no pong arrives within "WebSocketPingTimeout" seconds (0 = no check)
            // => dead connections (e.g. after a network failure) are detected
            int pingInterval = Configuration.GetValue<int>("WebSocketPingInterval", 60);
            int pingTimeout = Configuration.GetValue<int>("WebSocketPingTimeout", 60);
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = (pingInterval > 0) ? TimeSpan.FromSeconds(pingInterval) : TimeSpan.Zero
            };
            if (pingInterval > 0 && pingTimeout > 0)
            {
                webSocketOptions.KeepAliveTimeout = TimeSpan.FromSeconds(pingTimeout);
            }
            logger.LogInformation("Startup => WebSocket ping interval={0}s / timeout={1}s", pingInterval, (pingInterval > 0) ? pingTimeout : 0);

            // Accept WebSocket
            app.UseWebSockets(webSocketOptions);

            // Integrate custom OCPP middleware for message processing
            app.UseOCPPMiddleware();
        }

        /// <summary>
        /// Logs warnings at startup for an insecure configuration (API without key, chargepoints without authentication)
        /// </summary>
        private void LogSecurityWarnings(ILogger logger, OCPPCoreContext dbContext)
        {
            if (string.IsNullOrWhiteSpace(Configuration.GetValue<string>("ApiKey")))
            {
                if (Configuration.GetValue<bool>("AllowUnauthenticatedApi", false))
                {
                    logger.LogWarning("Startup => No 'ApiKey' configured and 'AllowUnauthenticatedApi' is active: the API is open for EVERY caller in the network!");
                }
                else
                {
                    logger.LogWarning("Startup => No 'ApiKey' configured: the API only accepts local requests. Configure an 'ApiKey' (identical in server and management).");
                }
            }

            try
            {
                int unsecuredChargePoints = dbContext.ChargePoints
                    .Count(cp => (cp.Username == null || cp.Username == "") && (cp.ClientCertThumb == null || cp.ClientCertThumb == ""));
                if (unsecuredChargePoints > 0)
                {
                    if (Configuration.GetValue<bool>("RequireChargePointAuthentication", false))
                    {
                        logger.LogWarning("Startup => {0} chargepoint(s) without username/password or certificate will be rejected ('RequireChargePointAuthentication' is active)", unsecuredChargePoints);
                    }
                    else
                    {
                        logger.LogWarning("Startup => {0} chargepoint(s) can connect without authentication. Configure username/password or a certificate for them.", unsecuredChargePoints);
                    }
                }
            }
            catch (Exception exp)
            {
                logger.LogError(exp, "Startup => Error checking chargepoint authentication: {0}", exp.Message);
            }
        }
    }
}
