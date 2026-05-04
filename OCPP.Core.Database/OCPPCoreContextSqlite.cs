using Microsoft.EntityFrameworkCore;

namespace OCPP.Core.Database
{
    public class OCPPCoreContextSqlite : OCPPCoreContext
    {
        public OCPPCoreContextSqlite(DbContextOptions<OCPPCoreContextSqlite> options)
            : base(options)
        {
        }
    }
}
