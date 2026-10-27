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
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Message dumps ("MessageDumpDir"): every OCPP message as a file (for debugging).
    /// File name: "yyyy-MM-dd_HH-mm-ss-ffff_[suffix].txt" (local time)
    /// </summary>
    public static class MessageDump
    {
        public const int DefaultRetentionDays = 7;

        private const string TimeFormat = "yyyy-MM-dd_HH-mm-ss-ffff";

        // only files with this name are deleted by the cleanup (the directory could also contain other files)
        private static readonly Regex DumpFileName = new Regex(@"^(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-\d{4})_[^\\/]+\.txt$", RegexOptions.Compiled);

        /// <summary>
        /// Full path of the dump directory (environment variables like %TEMP% expanded) or null if dumps are off
        /// </summary>
        public static string GetDirectory(IConfiguration configuration)
        {
            string dumpDir = configuration.GetValue<string>("MessageDumpDir");
            if (string.IsNullOrWhiteSpace(dumpDir)) return null;
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(dumpDir.Trim()));
        }

        /// <summary>
        /// Name of a new dump file
        /// </summary>
        public static string FileName(DateTime localTime, string nameSuffix)
        {
            return $"{localTime.ToString(TimeFormat, CultureInfo.InvariantCulture)}_{nameSuffix}.txt";
        }

        /// <summary>
        /// Deletes the dump files older than "retentionDays" (0 = keep forever) and returns their number.
        /// Only files with the name of a dump are deleted.
        /// </summary>
        public static int DeleteOldFiles(string directory, int retentionDays)
        {
            if (retentionDays <= 0 || string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return 0;

            // file names contain the local time
            DateTime olderThan = DateTime.Now.AddDays(-retentionDays);
            int deleted = 0;
            foreach (string file in Directory.EnumerateFiles(directory, "*.txt"))
            {
                Match match = DumpFileName.Match(Path.GetFileName(file));
                if (!match.Success) continue;
                if (!DateTime.TryParseExact(match.Groups[1].Value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)) continue;
                if (time >= olderThan) continue;

                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch (IOException)
                {
                    // in use => next time
                }
                catch (UnauthorizedAccessException)
                {
                    // no rights => next time
                }
            }
            return deleted;
        }
    }
}
