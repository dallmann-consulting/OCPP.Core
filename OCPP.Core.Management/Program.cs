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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using Serilog;

namespace OCPP.Core.Management
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                RunStartupTasksAsync(scope.ServiceProvider).GetAwaiter().GetResult();
            }

            host.Run();
        }

        private static async Task RunStartupTasksAsync(IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            var dbContext = services.GetRequiredService<OCPPCoreContext>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var configuration = services.GetRequiredService<IConfiguration>();

            await dbContext.EnsureMigratedAsync(logger);

            // Ensure the Admin role exists
            if (!await roleManager.RoleExistsAsync(Constants.AdminRoleName))
            {
                await roleManager.CreateAsync(new IdentityRole(Constants.AdminRoleName));
                logger.LogInformation("Created role '{Role}'", Constants.AdminRoleName);
            }

            // If no users exist yet, try to migrate from configuration / environment variables
            if (!await userManager.Users.AnyAsync())
            {
                var configUsers = configuration.GetSection("Users").GetChildren().ToList();
                if (configUsers.Count > 0)
                {
                    logger.LogInformation("Migrating {Count} user(s) from configuration into database...", configUsers.Count);
                    foreach (var cfgUser in configUsers)
                    {
                        var username = cfgUser.GetValue<string>("Username");
                        var password = cfgUser.GetValue<string>("Password");
                        var isAdmin = cfgUser.GetValue<bool>("Administrator");

                        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                        {
                            logger.LogWarning("Skipping incomplete user entry in configuration (missing Username or Password)");
                            continue;
                        }

                        var user = new IdentityUser { UserName = username, SecurityStamp = Guid.NewGuid().ToString() };
                        var result = await userManager.CreateAsync(user, password);
                        if (result.Succeeded)
                        {
                            if (isAdmin)
                                await userManager.AddToRoleAsync(user, Constants.AdminRoleName);
                            logger.LogInformation("Migrated user '{Username}' (admin={IsAdmin})", username, isAdmin);
                        }
                        else
                        {
                            logger.LogError("Failed to migrate user '{Username}': {Errors}",
                                username, string.Join(", ", result.Errors.Select(e => e.Description)));
                        }
                    }
                }
                else
                {
                    // No users anywhere → first-run setup mode
                    ApplicationState.IsFirstRun = true;
                    logger.LogInformation("No users found. Application is in first-run setup mode at /Account/Setup");
                }
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((ctx, config) => config
                    .ReadFrom.Configuration(ctx.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("CorrelationId", "none"))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
