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
                                                        _logger.LogInformation("OCPPMiddleware.Extensions => Extension '{0}' successfully initialized", extension.ExtensionName);
                                                        _rawMessageSinks.Add(extension);
                                                    }
                                                    else
                                                    {
                                                        _logger.LogError("OCPPMiddleware.Extensions => Extension '{0}' returned false on initialization", extension.ExtensionName);
                                                    }
                                                }
                                                catch (Exception exp)
                                                {
                                                    _logger.LogError(exp, "OCPPMiddleware.Extensions => Extension '{0}': Exception on initialization: {1}", type.FullName, exp.Message);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning("OCPPMiddleware.Extensions => Unable to create instance of IRawMessageSink '{0}'", type.FullName);
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogError("OCPPMiddleware.Extensions => Type is not assignable '{0}'", type.FullName);
                                        }
                                    }
                                }
                                catch(Exception exp)
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
        /// Sends an incoming raw message to all extensions
        /// </summary>
        private void ProcessRawIncomingMessageSinks(string ocppVersion, string chargerpointId, IOCPPMessage msg)
        {
            // Send raw incoming messages to extensions
            if (_rawMessageSinks != null && _rawMessageSinks.Count > 0)
            {
                foreach (IRawMessageSink msgSink in _rawMessageSinks)
                {
                    try
                    {
                        _logger.LogDebug("OCPPMiddleware => Sending message from charger '{0}' to extension '{1}'", chargerpointId, msgSink.ExtensionName);
                        msgSink.ReceiveIncomingMessage(ocppVersion, chargerpointId, msg);
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "OCPPMiddleware => Error sending message from charger '{0}' to extension '{1}': Error: {2}", chargerpointId, msgSink.ExtensionName, exp.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Sends an outgoing raw message to all extensions
        /// </summary>
        private void ProcessRawOutgoingMessageSinks(string ocppVersion, string chargerpointId, IOCPPMessage msg)
        {
            // Send raw outgoing messages to extensions
            if (_rawMessageSinks != null && _rawMessageSinks.Count > 0)
            {
                foreach (IRawMessageSink msgSink in _rawMessageSinks)
                {
                    try
                    {
                        _logger.LogDebug("OCPPMiddleware => Sending message to charger '{0}' to extension '{1}'", chargerpointId, msgSink.ExtensionName);
                        msgSink.ReceiveIncomingMessage(ocppVersion, chargerpointId, msg);
                    }
                    catch (Exception exp)
                    {
                        _logger.LogError(exp, "OCPPMiddleware => Error sending message to charger '{0}' to extension '{1}': Error: {2}", chargerpointId, msgSink.ExtensionName, exp.Message);
                    }
                }
            }
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
