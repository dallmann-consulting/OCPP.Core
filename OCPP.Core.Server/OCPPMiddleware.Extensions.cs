/*
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

using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        // List with extensions sinks for raw OCPP messages
        private List<Extensions.Interfaces.IRawMessageSink> _rawMessageSinks = new List<Extensions.Interfaces.IRawMessageSink>();

        // List with extensions sinks for external authorizations
        private List<Extensions.Interfaces.IExternalAuthorization> _authorizationSinks = new List<Extensions.Interfaces.IExternalAuthorization>();

        /// <summary>
        /// Load all extensions from extensions directory
        /// </summary>
        private void LoadExtensions()
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                DirectoryInfo dirInfo = Directory.GetParent(path);
                if (dirInfo != null)
                {
                    path = Path.Combine(dirInfo.FullName, "Extensions");
                    if (Directory.Exists(path))
                    {
                        string[] files = Directory.GetFiles(path);
                        foreach (string file in files)
                        {
                            // Check only DLLs with "Extension" in its name (improve performance)
                            if (file.EndsWith(".dll") && file.Contains("Extension"))
                            {
                                try
                                {
                                    ExtensionLoadContext loadContext = new ExtensionLoadContext(file);

                                    // load assembly
                                    //string assemblyName = Path.GetFileNameWithoutExtension(file);
                                    Assembly assembly = loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(file));
                                    _logger.LogInformation("OCPPMiddleware.Extensions => Loaded extension'{0}'", assembly.FullName);

                                    // iterate types and search for 
                                    foreach (Type type in assembly.GetTypes())
                                    {
                                        if (typeof(IRawMessageSink).IsAssignableFrom(type))
                                        {
                                            var t = Activator.CreateInstance(type);
                                            bool b = t is IRawMessageSink;
                                            IRawMessageSink extension = Activator.CreateInstance(type) as IRawMessageSink;
                                            if (extension != null)
                                            {
                                                _logger.LogDebug("OCPPMiddleware.Extensions => Created instance of IRawMessageSink '{0}'", extension.GetType().FullName);

                                                try
                                                {
                                                    if (extension.InitializeExtension(_logFactory, _configuration))
                                                    {
                                                        _logger.LogInformation("OCPPMiddleware.Extensions => Raw-Extension '{0}' successfully initialized", extension.ExtensionName);
                                                        _rawMessageSinks.Add(extension);
                                                    }
                                                    else
                                                    {
                                                        _logger.LogError("OCPPMiddleware.Extensions => Raw-Extension '{0}' returned false on initialization", extension.ExtensionName);
                                                    }
                                                }
                                                catch (Exception exp)
                                                {
                                                    _logger.LogError(exp, "OCPPMiddleware.Extensions => Raw-Extension '{0}': Exception on initialization: {1}", type.FullName, exp.Message);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning("OCPPMiddleware.Extensions => Unable to create instance of IRawMessageSink '{0}'", type.FullName);
                                            }
                                        }
                                        else if (typeof(IExternalAuthorization).IsAssignableFrom(type))
                                        {
                                            var t = Activator.CreateInstance(type);
                                            bool b = t is IExternalAuthorization;
                                            IExternalAuthorization extension = Activator.CreateInstance(type) as IExternalAuthorization;
                                            if (extension != null)
                                            {
                                                _logger.LogDebug("OCPPMiddleware.Extensions => Created instance of IExternalAuthorization '{0}'", extension.GetType().FullName);

                                                try
                                                {
                                                    if (extension.InitializeExtension(_logFactory, _configuration))
                                                    {
                                                        _logger.LogInformation("OCPPMiddleware.Extensions => Auth-Extension '{0}' successfully initialized", extension.ExtensionName);
                                                        _authorizationSinks.Add(extension);
                                                    }
                                                    else
                                                    {
                                                        _logger.LogError("OCPPMiddleware.Extensions => Auth-Extension '{0}' returned false on initialization", extension.ExtensionName);
                                                    }
                                                }
                                                catch (Exception exp)
                                                {
                                                    _logger.LogError(exp, "OCPPMiddleware.Extensions => Auth-Extension '{0}': Exception on initialization: {1}", type.FullName, exp.Message);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning("OCPPMiddleware.Extensions => Unable to create instance of IExternalAuthorization '{0}'", type.FullName);
                                            }
                                        }
                                    }
                                }
                                catch (Exception exp)
                                {
                                    _logger.LogError(exp, "OCPPMiddleware.Extensions => Exception loading file: '{0}' / Exp:{1}", file, exp.Message);
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogTrace("OCPPMiddleware.Extensions => No 'Extensions' folder");
                    }
                }
            }
            catch (Exception exp)
            {
                _logger.LogError(exp, "OCPPMiddleware.Extensions => Exception loading extensions: {0}", exp.Message);
            }
        }

        /// <summary>
        /// Sends token authorization request to registered extensions
        /// </summary>
        internal bool? ProcessExternalAuthorizations(AuthAction action, string token, string chargePointId, int? connectorId, string transactionId, string transactionStartToken)
        {
            if (_authorizationSinks != null && _authorizationSinks.Count > 0)
            {
                foreach (IExternalAuthorization extAuth in _authorizationSinks)
                {
                    try
                    {
                        _logger.LogDebug("OCPPMiddleware => Sending authorization request for token='{0}'; chargePointId='{1}'; connectorId='{2}'; transactionId='{3}'; startToken='{4}' to extension '{5}'", token, chargePointId, connectorId, transactionId, transactionStartToken, extAuth.ExtensionName);
                        bool? result = extAuth.Authorize(action, token, chargePointId, connectorId, transactionId, transactionStartToken);
                        _logger.LogDebug("OCPPMiddleware => Authorization result '{0}' for token='{1}'; chargePointId='{2}'; connectorId='{3}'; transactionId='{4}'; startToken='{5}' from extension '{6}'", result, token, chargePointId, connectorId, transactionId, transactionStartToken, extAuth.ExtensionName);

                        if (result.HasValue)
                        {
                            // Extension returned an explicit true/false result
                            // => exit loop and return result
                            return result.Value;
                        }
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "OCPPMiddleware => Error sending authorization request for token='{0}'; chargePointId='{1}'; connectorId='{2}'; transactionId='{3}'; startToken='{4}'  to extension '{5}' / Error: {6}", token, chargePointId, connectorId, transactionId, transactionStartToken, extAuth.ExtensionName, exp.Message);
                    }
                }
            }

            // No explicit result from any extension
            return null;
        }


        /// <summary>
        /// Sends an incoming raw message to all extensions
        /// </summary>
        private void ProcessRawIncomingMessageSinks(string ocppVersion, string chargepointId, IOCPPMessage msg)
        {
            // Send raw incoming messages to extensions
            if (_rawMessageSinks != null && _rawMessageSinks.Count > 0)
            {
                foreach (IRawMessageSink msgSink in _rawMessageSinks)
                {
                    try
                    {
                        _logger.LogDebug("OCPPMiddleware => Sending message from charger '{0}' to extension '{1}'", chargepointId, msgSink.ExtensionName);
                        msgSink.ReceiveIncomingMessage(ocppVersion, chargepointId, msg);
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "OCPPMiddleware => Error sending message from charger '{0}' to extension '{1}': Error: {2}", chargepointId, msgSink.ExtensionName, exp.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Sends an outgoing raw message to all extensions
        /// </summary>
        private void ProcessRawOutgoingMessageSinks(string ocppVersion, string chargepointId, IOCPPMessage msg)
        {
            // Send raw outgoing messages to extensions
            if (_rawMessageSinks != null && _rawMessageSinks.Count > 0)
            {
                foreach (IRawMessageSink msgSink in _rawMessageSinks)
                {
                    try
                    {
                        _logger.LogDebug("OCPPMiddleware => Sending message to charger '{0}' to extension '{1}'", chargepointId, msgSink.ExtensionName);
                        msgSink.ReceiveIncomingMessage(ocppVersion, chargepointId, msg);
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "OCPPMiddleware => Error sending message to charger '{0}' to extension '{1}': Error: {2}", chargepointId, msgSink.ExtensionName, exp.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Send authorization request to extensions
        /// </summary>
        private bool? ProcessExternalAuthorizationSinks(AuthAction action, string token, string chargePointId, int? connectorId, string transactionId, string transactionStartToken)
        {
            bool? result = null;

            // Send authorization request to extensions. When the first extension responds with "true" the loop is exited.
            if (_authorizationSinks != null && _authorizationSinks.Count > 0)
            {
                foreach (IExternalAuthorization authSink in _authorizationSinks)
                {
                    try
                    {
                        _logger.LogDebug("OCPPMiddleware => Sending authorization request for token '{0}' from charger '{1}'/connector '{2}'/tranaction '{3}'/transactionStartToken '{4}' to extension '{5}'", token, chargePointId, connectorId, transactionId, transactionStartToken, authSink.ExtensionName);
                        bool? authResult = authSink.Authorize(action, token, chargePointId, connectorId, transactionId, transactionStartToken);

                        if (authResult.HasValue)
                        {
                            result = authResult.Value;
                        }

                        if (result.HasValue && result.Value) break;
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "OCPPMiddleware => Error sending authorization for token '{0}' from charger '{1}' to extension '{2}': Error: {3}", token, chargePointId, authSink.ExtensionName, exp.Message);
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Helper class for resolving/loading references
    /// See https://learn.microsoft.com/de-de/dotnet/core/tutorials/creating-app-with-plugin-support
    /// </summary>
    internal class ExtensionLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver _resolver;

        public ExtensionLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
