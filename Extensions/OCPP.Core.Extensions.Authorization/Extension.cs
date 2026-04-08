using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Server.Extensions.Interfaces;

namespace OCPP.Core.Extensions.Authorization
{
    public class Extension : IExternalAuthorization
    {
        private ILoggerFactory? _logFactory;
        private ILogger? _logger;
        private IConfiguration? _ocppConfiguration;
        private IConfiguration? _extensionConfiguration;

        private const string  ExtName = "Authorization";

        /// <summary>
        /// Returnes a name of the extension (=> log output)
        /// </summary>
        public string ExtensionName
        {
            get { return ExtName; }
        }

        /// <summary>
        /// Initializes the extension
        /// </summary>
        /// <returns>Returns true when the initialization was successfull and the extension can be used</returns>
        public bool InitializeExtension(ILoggerFactory logFactory, IConfiguration configuration)
        {
            try
            {
                _logFactory = logFactory;
                _logger = logFactory.CreateLogger(ExtName);
                _ocppConfiguration = configuration;

                string? extensionPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                extensionPath = Path.GetDirectoryName(extensionPath);
                if (!string.IsNullOrEmpty(extensionPath))
                {
                    ConfigurationBuilder builder = new ConfigurationBuilder();
                    builder.SetBasePath(extensionPath);
                    builder.AddJsonFile("appsettingsAuthorization.json");
                    _extensionConfiguration = builder.Build();

                    _logger.LogTrace("InitializeExtension => Configuration loaded");
                }
                return true;
            }
            catch (Exception exp)
            {
                _logger?.LogError(exp, "InitializeExtension => Error initializing extension '{0}'", exp.Message);

                return false;
            }
        }

        /// <summary>
        /// Allows to override the internal authorization logic for trancations.
        /// If the method returns null, the standard logic is used.
        /// </summary>
        /// <returns>true if token is valid; false if token is NOT valid; null if default logic should be used</returns>
        public bool? Authorize(AuthAction action, string token, string chargePointId, int? connectorId, string transactionId, string transactionStartToken)
        {
            _logger?.LogDebug("Authorize => action='{0}'; token='{1}'; chargePointId='{2}'; connectorId='{3}'; transactionId='{4}'; transactionStartToken='{5}'", action, token, chargePointId, connectorId, transactionId, transactionStartToken);

            if (!string.IsNullOrEmpty(token))
            {
                if (_extensionConfiguration != null)
                {
                    string? good = _extensionConfiguration["good"];
                    string? bad = _extensionConfiguration["bad"];

                    if (string.Equals(token, good, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _logger?.LogInformation("Authorize GRANTED => token='{0}'", token);
                        return true;
                    }

                    if (string.Equals(token, bad, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _logger?.LogInformation("Authorize DENIED => token='{0}'", token);
                        return false;
                    }
                }
            }

            _logger?.LogInformation("Authorize NEUTRAL => token='{0}'", token);
            return null;
        }
    }
}
