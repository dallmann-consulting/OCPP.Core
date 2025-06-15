﻿/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2024 dallmann consulting GmbH.
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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Server.Extensions.Interfaces
{
    public interface IExternalAuthorization
    {
        /// <summary>
        /// Returnes a name of the extension (=> log output)
        /// </summary>
        string ExtensionName { get; }

        /// <summary>
        /// Initializes the extension
        /// </summary>
        /// <returns>Returns true when the initialization was successfull and the extension can be used</returns>
        bool InitializeExtension(ILoggerFactory logFactory, IConfiguration configuration);

        /// <summary>
        /// Allows to override the internal authorization logic for trancations.
        /// If the method returns null, the standard logic is used.
        /// </summary>
        bool? Authorize(AuthAction action, string token, string chargePointId, int? connectorId, string transactionId, string transactionStartToken);
    }

    public enum AuthAction
    {
        /// <summary>
        /// The authorization request is initiated by an OCPP Authorize message
        /// </summary>
        Authorize,

        /// <summary>
        /// The authorization request is initiated by an OCPP StartTransaction message
        /// </summary>
        StartStransaction,

        /// <summary>
        /// The authorization request is initiated by an OCPP StopTransaction message
        /// </summary>
        StopTransaction
    }
}
