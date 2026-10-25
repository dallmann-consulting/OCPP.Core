using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Hash format of charge point passwords
    /// </summary>
    public class ChargePointPasswordFormatTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("secret-password-123")]
        [InlineData("AQAAAAIAAYagAAAAE")]   // beginning of a hash, but too short
        public void Plaintext_IsNoHash(string? stored)
        {
            Assert.False(ChargePointPassword.IsHash(stored!));
        }

        [Fact]
        public void LongestPlaintext_IsNoHash()
        {
            // max. length => is always plaintext (also valid base64 with the hash format marker)
            byte[] bytes = new byte[36];
            bytes[0] = 0x01;
            Assert.False(ChargePointPassword.IsHash(Convert.ToBase64String(bytes)));
            Assert.False(ChargePointPassword.IsHash(new string('A', ChargePointPassword.MaxPlaintextLength)));
        }

        [Fact]
        public void Base64WithoutHashFormat_IsNoHash()
        {
            // 84 characters base64 like a hash, but without the format marker
            byte[] bytes = new byte[61];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i + 2);
            Assert.False(ChargePointPassword.IsHash(Convert.ToBase64String(bytes)));
        }

        [Fact]
        public void Hash_IsRecognizedAndVerified()
        {
            string hash = ChargePointPassword.Hash("secret:password");

            Assert.True(ChargePointPassword.IsHash(hash));
            Assert.NotEqual("secret:password", hash);
            Assert.True(ChargePointPassword.Verify(hash, "secret:password", out bool isPlaintext));
            Assert.False(isPlaintext);
            Assert.False(ChargePointPassword.Verify(hash, "wrong", out _));
            // the hash itself is not accepted as password
            Assert.False(ChargePointPassword.Verify(hash, hash, out _));
        }

        [Fact]
        public void Plaintext_IsVerifiedAndMarked()
        {
            Assert.True(ChargePointPassword.Verify("secret", "secret", out bool isPlaintext));
            Assert.True(isPlaintext);
            Assert.False(ChargePointPassword.Verify("secret", "Secret", out _));
            Assert.False(ChargePointPassword.Verify(null!, "", out _));
            Assert.False(ChargePointPassword.Verify("", "", out _));
        }
    }

    /// <summary>
    /// Basic authentication of charge points with hashed and plaintext passwords (#16, #19)
    /// </summary>
    public class ChargePointPasswordTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public ChargePointPasswordTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private string StoredPassword(string chargePointId)
        {
            using OCPPCoreContext dbContext = _factory.CreateDbContext();
            return dbContext.ChargePoints.AsNoTracking().Single(cp => cp.ChargePointId == chargePointId).Password;
        }

        [Fact]
        public async Task PlaintextPassword_IsReplacedByHashAtLogin()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            await using (ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "secret-password-123"))
            {
                Assert.Equal(WebSocketState.Open, cp.State);
            }

            string stored = StoredPassword(chargePointId);
            Assert.True(ChargePointPassword.IsHash(stored));

            // login with the hash works...
            await using (ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "secret-password-123"))
            {
                Assert.Equal(WebSocketState.Open, cp.State);
            }
            // ...and doesn't change it again
            Assert.Equal(stored, StoredPassword(chargePointId));

            // wrong password is still rejected
            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "wrong-password"));
            Assert.Contains("401", exp.Message);
        }

        [Fact]
        public async Task WrongPlaintextPassword_IsRejectedAndNotConverted()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "wrong-password"));

            Assert.Contains("401", exp.Message);
            Assert.Equal("secret-password-123", StoredPassword(chargePointId));
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task HashedPassword_Connects(string protocol)
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: ChargePointPassword.Hash("secret-password-123"));

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol, "user1", "secret-password-123");

            Assert.Equal(WebSocketState.Open, cp.State);
        }

        [Fact]
        public async Task WrongUsername_IsRejected()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: ChargePointPassword.Hash("secret-password-123"));

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user2", "secret-password-123"));

            Assert.Contains("401", exp.Message);
        }

        [Fact]
        public async Task PasswordWithColon_Connects()
        {
            // #19: only the first ':' separates user name and password
            string chargePointId = _factory.AddChargePoint(username: "user1", password: ChargePointPassword.Hash("pass:word:123"));

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "pass:word:123");

            Assert.Equal(WebSocketState.Open, cp.State);
        }

        [Theory]
        [InlineData("Basic")]
        [InlineData("Basic ")]
        [InlineData("Basic !!no-base64!!")]
        [InlineData("Bearer dXNlcjE6c2VjcmV0LXBhc3N3b3JkLTEyMw==")]   // correct credentials, wrong scheme
        [InlineData("Basic dXNlcjFzZWNyZXQtcGFzc3dvcmQtMTIz")]        // "user1secret-password-123" without ':'
        public async Task MalformedAuthorizationHeader_IsRejectedWith401(string header)
        {
            // #19: an invalid header must not cause an exception (=> 500)
            string chargePointId = _factory.AddChargePoint(username: "user1", password: ChargePointPassword.Hash("secret-password-123"));

            WebSocketClient client = _factory.Server.CreateWebSocketClient();
            client.SubProtocols.Add(OcppProtocols.Ocpp16);
            client.ConfigureRequest = request => request.Headers["Authorization"] = header;

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.ConnectAsync(new Uri(_factory.Server.BaseAddress, "OCPP/" + chargePointId), CancellationToken.None));

            Assert.Contains("401", exp.Message);
        }

        [Fact]
        public void BasicAuthenticationHeader_IsParsed()
        {
            string header = "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user1:pä:ss"));

            Assert.True(OCPPMiddleware.TryReadBasicAuthentication(header, out string username, out string password));
            Assert.Equal("user1", username);
            Assert.Equal("pä:ss", password);

            Assert.False(OCPPMiddleware.TryReadBasicAuthentication(null!, out _, out _));
        }
    }

    /// <summary>
    /// Database without the migration "ChargePointPasswordHash" (e.g. no rights for automatic migrations)
    /// </summary>
    public class PendingPasswordMigrationTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public PendingPasswordMigrationTests(OcppServerFactory factory)
        {
            _factory = factory;

            // simulate the old schema state: migration not in the history
            using OCPPCoreContext dbContext = _factory.CreateDbContext();
            dbContext.Database.ExecuteSqlRaw("DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%_ChargePointPasswordHash'");
        }

        [Fact]
        public async Task PlaintextPassword_StaysPlaintext()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: "secret-password-123");

            using (OCPPCoreContext dbContext = _factory.CreateDbContext())
            {
                Assert.Contains(dbContext.GetPendingMigrationNames(), m => m.EndsWith("_" + MigrationExtensions.ChargePointPasswordHashMigration));
            }

            await using (ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "secret-password-123"))
            {
                Assert.Equal(WebSocketState.Open, cp.State);
            }

            using (OCPPCoreContext dbContext = _factory.CreateDbContext())
            {
                // the old column (50 characters) can't store the hash => no conversion
                Assert.Equal("secret-password-123", dbContext.ChargePoints.AsNoTracking().Single(cp => cp.ChargePointId == chargePointId).Password);
            }
        }
    }
}
