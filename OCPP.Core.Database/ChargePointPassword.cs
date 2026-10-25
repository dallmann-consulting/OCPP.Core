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
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace OCPP.Core.Database
{
    /// <summary>
    /// Passwords of charge points (basic authentication), stored as hash (same procedure as the management users).
    /// Old passwords can still be stored as plaintext (before the migration "ChargePointPasswordHash" or before
    /// the first successful login) => both formats are supported and recognized.
    /// </summary>
    public static class ChargePointPassword
    {
        /// <summary>
        /// Max. length of a plaintext password (input). A hash is always longer (84 characters)
        /// => a plaintext password can't be mistaken for a hash.
        /// </summary>
        public const int MaxPlaintextLength = 50;

        // Identity hash format V3: 1 byte marker (0x01), 4 bytes PRF, 4 bytes iteration count, 4 bytes salt length, salt, 32 bytes subkey
        private const byte FormatMarkerV3 = 0x01;
        private const int SubkeyLength = 32;

        private static readonly PasswordHasher<ChargePoint> Hasher = new PasswordHasher<ChargePoint>();

        /// <summary>
        /// True if the stored value is a password hash (otherwise plaintext)
        /// </summary>
        public static bool IsHash(string storedPassword)
        {
            if (string.IsNullOrEmpty(storedPassword) || storedPassword.Length <= MaxPlaintextLength) return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(storedPassword);
            }
            catch (FormatException)
            {
                return false;
            }

            if (bytes.Length < 13 || bytes[0] != FormatMarkerV3) return false;
            uint prf = ReadUInt32BigEndian(bytes, 1);
            uint iterations = ReadUInt32BigEndian(bytes, 5);
            uint saltLength = ReadUInt32BigEndian(bytes, 9);
            return prf <= 2 &&
                   iterations > 0 &&
                   saltLength >= 16 &&
                   bytes.Length == 13 + saltLength + SubkeyLength;
        }

        /// <summary>
        /// Creates the hash of a password
        /// </summary>
        public static string Hash(string password)
        {
            return Hasher.HashPassword(null, password);
        }

        /// <summary>
        /// Checks a password against the stored value (hash or plaintext).
        /// "isPlaintext": the stored value is plaintext => should be replaced by a hash.
        /// </summary>
        public static bool Verify(string storedPassword, string password, out bool isPlaintext)
        {
            isPlaintext = false;
            if (string.IsNullOrEmpty(storedPassword) || password == null) return false;

            if (IsHash(storedPassword))
            {
                PasswordVerificationResult result = Hasher.VerifyHashedPassword(null, storedPassword, password);
                return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
            }

            isPlaintext = true;
            return FixedTimeEquals(storedPassword, password);
        }

        /// <summary>
        /// Compares two strings in constant time (no timing attacks)
        /// </summary>
        public static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }
    }
}
