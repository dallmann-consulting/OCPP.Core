#!/bin/sh

cd /app/server
dotnet OCPP.Core.Server.dll &
SERVER_PID=$!

cd /app/management
dotnet OCPP.Core.Management.dll &
MGMT_PID=$!

trap "kill $SERVER_PID $MGMT_PID" TERM INT

wait
