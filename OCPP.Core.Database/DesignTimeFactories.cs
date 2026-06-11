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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OCPP.Core.Database
{
    public class OCPPCoreContextSqliteFactory : IDesignTimeDbContextFactory<OCPPCoreContextSqlite>
    {
        public OCPPCoreContextSqlite CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OCPPCoreContextSqlite>();
            optionsBuilder.UseSqlite("Filename=./designtime.sqlite;");
            return new OCPPCoreContextSqlite(optionsBuilder.Options);
        }
    }

    public class OCPPCoreContextSqlServerFactory : IDesignTimeDbContextFactory<OCPPCoreContextSqlServer>
    {
        public OCPPCoreContextSqlServer CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OCPPCoreContextSqlServer>();
            optionsBuilder.UseSqlServer("Server=.;Database=OCPP.Core;Trusted_Connection=True;Encrypt=false;");
            return new OCPPCoreContextSqlServer(optionsBuilder.Options);
        }
    }
}
