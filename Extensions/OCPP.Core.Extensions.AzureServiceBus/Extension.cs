using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using OCPP.Core.Server.Extensions.Interfaces;

namespace OCPP.Core.Extensions.AzureServiceBus
{
    public class Extension : IRawMessageSink
    {
        private ILoggerFactory? _logFactory;
        private ILogger? _logger;
        private IConfiguration? _ocppConfiguration;
        private IConfiguration? _extensionConfiguration;

        private ServiceBusClient? _serviceBusClient;
        private string? _serviceBusQueue;

        private const string  ExtName = "AzureServiceBus";

        public string ExtensionName
        {
            get { return ExtName; }
        }

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
                    builder.AddJsonFile("appsettings.json");
                    _extensionConfiguration = builder.Build();

                    _logger.LogTrace("InitializeExtension => Configuration loaded");

                    // Connect to Azure Service Bus if connectionstring is provided
                    var serviceBusConnectionString = _extensionConfiguration?.GetConnectionString("ServiceBusEndpoint");
                    if (!string.IsNullOrEmpty(serviceBusConnectionString))
                    {
                        _serviceBusClient = new ServiceBusClient(serviceBusConnectionString, new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });
                    }
                    else
                    {
                        _logger?.LogError("InitializeExtension => Configuration entry 'ServiceBusEndpoint' missing");
                    }

                    if (_extensionConfiguration != null)
                        _serviceBusQueue = _extensionConfiguration["ServiceBusQueue"];
                }
                return true;
            }
            catch (Exception exp)
            {
                _logger?.LogError(exp, "InitializeExtension => Error initializing extension '{0}'", exp.Message);

                return false;
            }
        }

        public void ReceiveIncomingMessage(string ocppVersion, string chargePointId, IOCPPMessage rawMessage)
        {
            _logger?.LogDebug("ReceiveIncomingMessage(ocppVersion='{0}', chargePointId='{1}', rawMessage: {2}", ocppVersion, chargePointId, rawMessage);
            ForwardMessageToAzureServiceBus(ocppVersion, chargePointId, rawMessage).Wait();
        }

        public void ReceiveOutgoingMessage(string ocppVersion, string chargePointId, IOCPPMessage rawMessage)
        {
            _logger?.LogDebug("ReceiveOutgoingMessage(ocppVersion='{0}', chargePointId='{1}', rawMessage: {2}", ocppVersion, chargePointId, rawMessage);
            ForwardMessageToAzureServiceBus(ocppVersion, chargePointId, rawMessage).Wait();
        }

        private async Task ForwardMessageToAzureServiceBus(string ocppVersion, string chargePointId, IOCPPMessage rawMessage)
        {
            if (_serviceBusClient == null || string.IsNullOrEmpty(_serviceBusQueue))
                return;

            ServiceBusSender sender = _serviceBusClient.CreateSender(_serviceBusQueue);

            var forwardObject = new
            {
                OCPPVersion = ocppVersion,
                ChargePointId = chargePointId,
                RawMessage = rawMessage
            };
            var serializedMessage = JsonConvert.SerializeObject(forwardObject);
            var messageBody = new ServiceBusMessage(Encoding.UTF8.GetBytes(serializedMessage));
            try
            {
                await sender.SendMessageAsync(messageBody);
            }
            catch (Exception exp)
            {
                _logger?.LogError(exp, "Exception while forwarding message to Azure Service Bus: {0}", exp.Message);
            }
        }
    }
}
