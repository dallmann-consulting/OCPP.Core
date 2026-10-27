using Microsoft.Extensions.Configuration;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Message dumps ("MessageDumpDir"): environment variables, directory creation, deletion of old files
    /// </summary>
    public class MessageDumpTests
    {
        private static IConfiguration Config(string? dumpDir) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MessageDumpDir"] = dumpDir })
            .Build();

        [Fact]
        public void Directory_EnvironmentVariablesAreExpanded()
        {
            string variable = "OCPP_CORE_TEST_" + Guid.NewGuid().ToString("N");
            string baseDir = Path.Combine(Path.GetTempPath(), "ocpp-core-dumpbase");
            Environment.SetEnvironmentVariable(variable, baseDir);
            try
            {
                string? dir = MessageDump.GetDirectory(Config($"%{variable}%{Path.DirectorySeparatorChar}OCPP"));
                Assert.Equal(Path.Combine(baseDir, "OCPP"), dir);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Directory_Empty_DumpsAreOff(string? dumpDir)
        {
            Assert.Null(MessageDump.GetDirectory(Config(dumpDir)));
        }

        [Fact]
        public void DeleteOldFiles_OnlyOldDumpFiles()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ocpp-core-dump-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string oldDump = Path.Combine(dir, MessageDump.FileName(DateTime.Now.AddDays(-10), "CP1_in"));
                string newDump = Path.Combine(dir, MessageDump.FileName(DateTime.Now.AddDays(-1), "CP1_out"));
                // other files in the directory are never deleted (also when they are old)
                string foreign = Path.Combine(dir, "notes.txt");
                string foreignWithDate = Path.Combine(dir, "2020-01-01_backup.txt");
                foreach (string f in new[] { oldDump, newDump, foreign, foreignWithDate })
                {
                    File.WriteAllText(f, "x");
                    File.SetLastWriteTime(f, DateTime.Now.AddDays(-30));
                }

                // 0 days = keep forever
                Assert.Equal(0, MessageDump.DeleteOldFiles(dir, 0));
                Assert.True(File.Exists(oldDump));

                Assert.Equal(1, MessageDump.DeleteOldFiles(dir, 7));
                Assert.False(File.Exists(oldDump));
                Assert.True(File.Exists(newDump));
                Assert.True(File.Exists(foreign));
                Assert.True(File.Exists(foreignWithDate));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }

    /// <summary>
    /// Server with message dumps into a directory that doesn't exist yet (path with environment variable)
    /// </summary>
    public class MessageDumpServerFactory : OcppServerFactory
    {
        public static readonly string Variable = "OCPP_CORE_TEST_DUMP_" + Guid.NewGuid().ToString("N");
        public static readonly string BaseDir = Path.Combine(Path.GetTempPath(), "ocpp-core-dump-" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            Environment.SetEnvironmentVariable(Variable, BaseDir);
            settings["MessageDumpDir"] = $"%{Variable}%{Path.DirectorySeparatorChar}dumps";
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            try
            {
                Directory.Delete(BaseDir, true);
            }
            catch (IOException)
            {
            }
            Environment.SetEnvironmentVariable(Variable, null);
        }
    }

    public class MessageDumpServerTests : IClassFixture<MessageDumpServerFactory>
    {
        private readonly MessageDumpServerFactory _factory;

        public MessageDumpServerTests(MessageDumpServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Messages_AreDumpedIntoTheCreatedDirectory()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            await cp.CallAsync("Heartbeat", new { });

            // incoming messages are dumped asynchronously => wait a little
            string dir = Path.Combine(MessageDumpServerFactory.BaseDir, "dumps");
            bool dumped = false;
            for (int i = 0; i < 50 && !dumped; i++)
            {
                dumped = Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any(f => File.ReadAllText(f).Contains("Heartbeat"));
                if (!dumped) await Task.Delay(100, TestContext.Current.CancellationToken);
            }
            Assert.True(dumped);
        }
    }
}
