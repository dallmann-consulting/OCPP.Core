# OCPP.Core Server API
Most messages are initiated by the chargers. But some messages are initiated by the OCPP backend.
OCPP.Core currently supports:
* Reset
* UnlockConnector
* SetChargingProfile (not verified)
* ClearChargingProfile (not verified)

The OCPP.Core.Server offers an API for using these messages. Additionally the server supports a status request for the online status of all connected chargers.
The management UI uses this API as well.

## API format

The REST-API uses the following format:

    /API/<command>[/chargepointId[/connectorId[/parameter]]]

For authentication/authorization purposes the API uses an API-key (like a password). This key must be send as an http header "X-API-Key" - see [here](https://swagger.io/docs/specification/authentication/api-keys/).
The allowed key is configured in the appsettings.json.


## Functions

### Status
Request the status of all connected chargers and connectors

	/API/Status

The answer should be (example):
    [
        {
            "id": "station42",
            "name": "myWallbox",
            "protocol": "ocpp1.6",
            "OnlineConnectors": {
                "1": {
                    "Status": 1,
                    "ChargeRateKW": null,
                    "MeterKWH": null,
                    "SoC": null
                }
            }
        }
    ]

### Reset
Initiates a reset/reboot of the charger.

	/API/Reset/station42

The answer should be:
{"status"="Accepted"} or {"status"="Rejected"}
or with OCPP 2.x {"status"="Scheduled"}


### UnlockConnector
Send an unlock request for a certain connector

	/API/UnlockConnector/station42/1

The answer should be:
{"status"="Unlocked"} or {"status"="UnlockFailed"}
or  
OCPP1.6 {"status"="NotSupported"}
OCPP2.x {"status"="OngoingAuthorizedTransaction"} or {"status"="UnknownConnector"}


### SetChargingProfile
Sets a charging limit (power) for a certain connector. OCPP.Core does not support schedules. It sets the specified limit as a simple daily 24h schedule (=constant limit).

	/API/SetChargingLimit/station42/1/2000W 
or

	/API/SetChargingLimit/station42/1/16A

The answer should be:
{"status"="Accepted"} or {"status"="Rejected"} or 
OCPP1.6 {"status"="NotSupported"}

Comment:
Our Keba chargers are rejecting limits for specific connectors. But they accept connectorId=0 as the setting for all connectors.


### ClearChargingProfile
Clears the charging limit (power) for a certain connector.

	/API/ClearChargingLimit/station42/1"

The answer should be:
 {"status"="Accepted"} or {"status"="Unknown"}
