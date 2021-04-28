# Real life experiences with KEBA

As stated in the [ReadMe](README.md) we are using OCPP.Core in our multi family house with 4 [KEBA P30c/x](https://www.keba.com/de/emobility/products/c-series/c-serie)
charge points operating in a load management network. This works with one P30**X** operating as the master and up to 15 P30**C** operating as slaves. 

Only the P30**X** has a Web-UI and only this device connects to the OCPP server. All charge points in the load management network are referenced as different connectors!
So, from the point of view of the OCPP server, we have a single charging station with 4 connectors (cables).

Here is an extract of the first real messages:

```
[2,"a8141dd9-4904-4443-976b-bf032324a52e","BootNotification",{"chargePointVendor":"Keba AG","chargePointModel":"KC-P30-EC2401B2-M0R","chargePointSerialNumber":"dummy1","chargeBoxSerialNumber":"dummy1","firmwareVersion":"1.12.0"}]
...
[2,"40ebd0b0-11ba-4a09-ad19-7e7a0a0b25c5","StatusNotification",{"connectorId":1,"status":"Available","errorCode":"NoError","timestamp":"2021-04-07T07:52:10.462Z","vendorId":"1.12.0"}]
[2,"cee47d29-fc49-4517-af15-2958fc250f43","StatusNotification",{"connectorId":2,"status":"Available","errorCode":"NoError","timestamp":"2021-04-07T07:52:11.864Z","vendorId":"1.12.0"}]
[2,"3dfa7fe7-1cbf-44e2-9b30-cbb5b40da7db","StatusNotification",{"connectorId":3,"status":"Available","errorCode":"NoError","timestamp":"2021-04-07T07:52:12.672Z","vendorId":"1.12.0"}]	
[2,"5b1146f0-a252-4370-b498-8bb2fa41f62b","StatusNotification",{"connectorId":4,"status":"Available","errorCode":"NoError","timestamp":"2021-04-07T07:52:13.200Z","vendorId":"1.12.0"}]
...
[2,"6cbbd7f4-c9cc-4ca3-b2c4-87f7e113a8ae","MeterValues",{"connectorId":1,"meterValue":[{"timestamp":"2021-04-07T11:00:00.000Z","sampledValue":[{"value":"236.1","context":"Sample.Clock","format":"Raw","measurand":"Energy.Active.Import.Register","location":"Outlet","unit":"Wh"}]}]}]
[2,"db6bc0c5-7e43-4c24-a9e2-e18248057735","MeterValues",{"connectorId":2,"meterValue":[{"timestamp":"2021-04-07T11:00:00.000Z","sampledValue":[{"value":"162.3","context":"Sample.Clock","format":"Raw","measurand":"Energy.Active.Import.Register","location":"Outlet","unit":"Wh"}]}]}]
[2,"2468cc5a-403f-472c-a5c5-e88bc5f81c8e","MeterValues",{"connectorId":3,"meterValue":[{"timestamp":"2021-04-07T11:00:00.000Z","sampledValue":[{"value":"171.7","context":"Sample.Clock","format":"Raw","measurand":"Energy.Active.Import.Register","location":"Outlet","unit":"Wh"}]}]}]
[2,"69a5d1b3-4020-48f5-ad79-f6869b644cbd","MeterValues",{"connectorId":4,"meterValue":[{"timestamp":"2021-04-07T11:00:00.000Z","sampledValue":[{"value":"164.5","context":"Sample.Clock","format":"Raw","measurand":"Energy.Active.Import.Register","location":"Outlet","unit":"Wh"}]}]}]
[2,"19d6e3cf-535f-4b83-b1b0-7f16cee6e317","MeterValues",{"connectorId":0,"meterValue":[{"timestamp":"2021-04-07T11:00:00.000Z","sampledValue":[{"value":"734.6","context":"Sample.Clock","format":"Raw","measurand":"Energy.Active.Import.Register","location":"Outlet","unit":"Wh"}]}]}]
```

So we have one charge point with 4 connectors and 4 meters. The connector "0" is the sum of all 4 meter values.


And this is the first charging transaction message:

```
[2,"dab92753-5fe0-43cd-84d1-44cb46d25951","StartTransaction",{"connectorId":4,"idTag":"12345678_dummy1","timestamp":"2021-04-08T18:49:11.450Z","meterStart":164}]
```

Have a look at the RFID tag: "12345678_dummy1"

Because the whole network operates as a single charge point, there is no possibilty to authenticate RFID tags on different charge points.
So KEBA decided to append the charge points serial number to the RFID number and send that as the idTag.


I configured the charge points to use its local RFID authentication. You can allow/deny access for each RFID tag and each charge point there.
But the transaction messages also send an RFID tag. If the OCPP server responds with a blocked, invalid etc. status, the master charge point immediately sets that RFID tag to blocked or expired.
I wasn't prepared for the combined idTags and it took me a while to diagnose this problem! You authenticate successfully with your RFID tag and the EV and the charge point communicate. When the charge point (network) send the "StartTransaction" message the OCPP server does **NOT** find an RFID tag with an appended serial number.
So it responds with an "invalid" status and the charge point stops the process with "De-Authorized". And then the RFID tag is blocked in the charge points... :-(


Now the OCPP server cuts off any "_xxx" suffix from the submitted idTags.


With Version 1.1.0 I added multi connector support (see ReadMe) and this works fine. Every connector has a custom name defined, so that it looks like they are independent charge points.


