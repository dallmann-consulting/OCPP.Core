using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// TransactionEvent (2.x) finds transactions by charge point + Uid => index instead of a full scan
    /// </summary>
    public class TransactionUidIndexTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TransactionUidIndexTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void IndexExists()
        {
            using OCPPCoreContext dbContext = _factory.CreateDbContext();

            Assert.Contains(dbContext.Database.GetAppliedMigrations(), m => m.EndsWith("_TransactionsUidIndex"));
            Assert.Equal(1, ExecuteScalar(dbContext, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Transactions_ChargePointId_Uid'"));
        }

        [Fact]
        public void FindByUid_UsesIndex()
        {
            using OCPPCoreContext dbContext = _factory.CreateDbContext();

            // same query as ControllerBase.FindTransactionByUid
            string plan = QueryPlan(dbContext,
                "SELECT * FROM \"Transactions\" WHERE \"ChargePointId\" = 'CP1' AND \"Uid\" = 'UID1' ORDER BY \"TransactionId\" DESC LIMIT 1");

            Assert.Contains("IX_Transactions_ChargePointId_Uid", plan);
        }

        [Fact]
        public void DuplicateUid_IsStillPossible()
        {
            // not unique: old data may contain duplicates, the migration must not fail
            string chargePointId = _factory.AddChargePoint();
            using OCPPCoreContext dbContext = _factory.CreateDbContext();
            for (int i = 0; i < 2; i++)
            {
                dbContext.Transactions.Add(new Transaction()
                {
                    ChargePointId = chargePointId,
                    ConnectorId = 1,
                    Uid = "DUPLICATE-UID",
                    StartTime = DateTime.UtcNow,
                    MeterStart = 0
                });
            }

            dbContext.SaveChanges();

            Assert.Equal(2, dbContext.Transactions.Count(t => t.ChargePointId == chargePointId && t.Uid == "DUPLICATE-UID"));
        }

        private static string QueryPlan(OCPPCoreContext dbContext, string sql)
        {
            dbContext.Database.OpenConnection();
            using DbCommand cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
            using DbDataReader reader = cmd.ExecuteReader();
            List<string> details = new List<string>();
            while (reader.Read())
            {
                // columns: id, parent, notused, detail
                details.Add(reader.GetString(3));
            }
            return string.Join(" | ", details);
        }

        private static long ExecuteScalar(OCPPCoreContext dbContext, string sql)
        {
            dbContext.Database.OpenConnection();
            using DbCommand cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
}
