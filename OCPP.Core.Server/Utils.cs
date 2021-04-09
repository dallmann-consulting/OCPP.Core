/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
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

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace OCPP.Core.Server
{
    public class Utils
    {
        /// <summary>
        /// Clean charge tag Id from possible suffix ("..._abc")
        /// </summary>
        public static string CleanChargeTagId(string rawChargeTagId, ILogger logger)
        {
            string idTag = rawChargeTagId;

            // KEBA adds the serial to the idTag ("<idTag>_<serial>") => cut off suffix
            if (!string.IsNullOrWhiteSpace(rawChargeTagId))
            {
                int sep = rawChargeTagId.IndexOf('_');
                if (sep >= 0)
                {
                    idTag = rawChargeTagId.Substring(0, sep);
                    logger.LogTrace("CleanChargeTagId => Charge tag '{0}' => '{1}'", rawChargeTagId, idTag);
                }
            }

            return idTag;
        }

        public static DateTimeOffset MaxExpiryDate
        {
            get
            {
                return new DateTime(2199, 12, 31);
            }
        }
    }
}
