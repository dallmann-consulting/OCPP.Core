using Microsoft.EntityFrameworkCore;

namespace OCPP.Core.Database
{
    public class OCPPCoreContextSqlServer : OCPPCoreContext
    {
        public OCPPCoreContextSqlServer(DbContextOptions<OCPPCoreContextSqlServer> options)
            : base(options)
        {
        }
    }
}
