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



### RemoteStartTransaction
Request the charger to (remotely) start a transaction (simply explained: virtually presenting a specific charge tag to the charger)

	/API/RemoteStartTransaction/station42/1/tag1234

The answer should be:
{"status"="Accepted"} or {"status"="Rejected"}


### RemoteStopTransaction
Request the charger to end a specific transaction.

	/API/RemoteStopTransaction/station42/1

The answer should be:
{"status"="Accepted"} or {"status"="Rejected"}
The server checks the last transaction for the specified connector and return the http code 424 (FailedDependency) when no open transaction was found.


### GetVariables / SetVariables

Reads or changes configuration values of a charge point. The API follows the
OCPP 2.x device model: a value is addressed by a **component** and a **variable**
(both with an optional instance, the component optionally with an EVSE).

OCPP 1.6 has no device model and is mapped as a special case: the component is
left out and the variable name is the 1.6 configuration key. The server translates
the request into GetConfiguration/ChangeConfiguration (1.6) or
GetVariables/SetVariables (2.0.1/2.1).

Short form (GET), key = `[Component.]Variable`:

```
/API/GetVariables/station42/OCPPCommCtrlr.HeartbeatInterval      (OCPP 2.x)
/API/GetVariables/station42/HeartbeatInterval                    (OCPP 1.6)
/API/GetVariables/station42                                      (OCPP 1.6: all keys)
/API/SetVariables/station42/OCPPCommCtrlr.HeartbeatInterval/240  (OCPP 2.x)
/API/SetVariables/station42/HeartbeatInterval/240                (OCPP 1.6)
```

Full form (POST with JSON body), e.g. for several values at once, instances,
EVSEs, attribute types or values containing "/":

```
POST /API/SetVariables/station42
{
  "variables": [
    {
      "component": { "name": "SampledDataCtrlr" },
      "variable": { "name": "TxUpdatedInterval" },
      "value": "60"
    },
    {
      "component": { "name": "OCPPCommCtrlr" },
      "variable": { "name": "HeartbeatInterval" },
      "value": "240"
    }
  ]
}
```

`component.instance`, `component.evse` (`id`, `connectorId`), `variable.instance`
and `attributeType` (Actual (default), Target, MinSet, MaxSet) are optional.
`value` is required for SetVariables and ignored for GetVariables. GetVariables
without variables (short form without key or empty body) requests all values.

The answer contains one result per variable:

```
{
  "variables": [
    {
      "component": { "name": "OCPPCommCtrlr" },
      "variable": { "name": "HeartbeatInterval" },
      "value": "300",
      "status": "Accepted",
      "mutability": "ReadWrite"
    }
  ]
}
```

`status` is one of the OCPP 2.x values: Accepted, Rejected, UnknownComponent,
UnknownVariable, NotSupportedAttributeType, RebootRequired (SetVariables only) -
or "Timeout"/"Disconnected" if the charge point didn't answer. `statusInfo` contains optional
details. `value` is only returned by GetVariables. `mutability` (ReadOnly, WriteOnly,
ReadWrite) is returned when reading all values; with OCPP 1.6 always.

#### Reading all values

```
/API/GetVariables/station42
/API/GetVariables/station42?reportBase=FullInventory
POST /API/GetVariables/station42   { "reportBase": "FullInventory" }
```

OCPP 1.6 returns all configuration keys (GetConfiguration without key).

OCPP 2.x sends a GetBaseReport and collects the report parts (NotifyReport messages)
the charge point sends afterwards. `reportBase` is ConfigurationInventory (default),
FullInventory or SummaryInventory. The results additionally contain the
characteristics of the variables (`dataType`, `unit`, `minLimit`, `maxLimit`, `valuesList`).

If the result is not complete, the answer contains a top level `status` (and the
values received so far):
* "Timeout" - the charge point didn't answer or sent no report part
* "Incomplete" - report parts are missing (`missingSeqNo`) or the last part didn't arrive
* "Disconnected" - the charge point disconnected while sending the report
* "TooLarge" - the report exceeds `ReportMaxItems`
* "Rejected" / "NotSupported" - the charge point declined the report (`statusInfo`)

```
{
  "status": "Incomplete",
  "missingSeqNo": [ 1 ],
  "variables": [ ... ]
}
```

The server waits until the last part arrives, but at most `ReportIdleTimeout`
seconds without a new part (default 30) and `ReportMaxDuration` seconds in total
(default 300) - see appsettings.json.

Mapping for OCPP 1.6:
* Entries with a component, a variable instance or an attribute type other than
  "Actual" are not sent to the charge point and get the status UnknownComponent,
  UnknownVariable or NotSupportedAttributeType.
* Keys listed in "unknownKey" and the ChangeConfiguration status "NotSupported"
  are returned as UnknownVariable.
* ChangeConfiguration only supports a single key. SetVariables therefore sends one
  request per entry. After a timeout the remaining entries are not sent and get the
  status "Timeout".
* `reportBase` is ignored.

Mapping for OCPP 2.x:
* Entries without a component get the status UnknownComponent (OCPP 2.x requires a component).

### In general
These commands means that the server send a request to the charger and the charger needs to answer in a reasonable period
of time. The server can not wait indefinitely and the OCPP server waits for 60 seconds.
After that the API caller will get the response {"status"="Timeout"}.
If the charger disconnects while the server waits for the answer, the API caller immediately gets
the response {"status"="Disconnected"}.