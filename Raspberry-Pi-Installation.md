# Installing OCPP.Core on a Raspberry Pi

This guide covers installing OCPP.Core (Server + Management Web UI) on a Raspberry Pi running Raspberry Pi OS (64-bit, Debian-based) with SQLite as the database.

## Prerequisites

- Raspberry Pi 4 or 5 (recommended, ARM64)
- Raspberry Pi OS Lite or Desktop, **64-bit** (Bookworm)
- Internet access on the Raspberry Pi
- SSH access or direct console
- At least 1 GB free disk space

---

## Step 1: Install .NET 10 Runtime

Microsoft provides an installation script that automatically sets up the current .NET runtime:

```bash
# Install dependencies
sudo apt-get update
sudo apt-get install -y curl unzip libicu-dev

# Download and run the .NET install script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime aspnetcore

# Set the PATH permanently
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

Verify the installation:

```bash
dotnet --version
# Should output something like "10.0.x"
```

---

## Step 2: Download OCPP.Core

Both options result in the same layout: `/opt/ocpp/server` and `/opt/ocpp/management`.

### Option A: Release ZIP (recommended)

The release files contain the version in their name. Look up the latest version on the [release page](https://github.com/dallmann-consulting/OCPP.Core/releases) and set it below:

```bash
# Create directory
sudo mkdir -p /opt/ocpp
sudo chown $USER:$USER /opt/ocpp
cd /opt/ocpp

# Download the release (adjust the version)
VERSION=V1.8.0
curl -L "https://github.com/dallmann-consulting/OCPP.Core/releases/download/${VERSION}/OCPP.Core.${VERSION}.Portable.Framework.Dependent.zip" -o ocpp.zip
unzip ocpp.zip -d .
rm ocpp.zip

# The ZIP contains the folders "OCPP.Core.Server" and "OCPP.Core.Management"
# => rename them to the paths used in this guide
mv OCPP.Core.Server server
mv OCPP.Core.Management management
```

### Option B: Build from source

If you want to build from source, you need the .NET SDK in addition to the runtime:

```bash
# Install .NET SDK (instead of just the runtime)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0
source ~/.bashrc

# Clone the repository
cd /opt
sudo git clone https://github.com/dallmann-consulting/OCPP.Core.git ocpp
sudo chown -R $USER:$USER /opt/ocpp
cd /opt/ocpp

# Build and publish
dotnet publish OCPP.Core.Server/OCPP.Core.Server.csproj -c Release -o /opt/ocpp/server
dotnet publish OCPP.Core.Management/OCPP.Core.Management.csproj -c Release -o /opt/ocpp/management
```

---

## Step 3: Set Up Directories

```bash
# Create the SQLite database directory
mkdir -p /opt/ocpp/data/SQLite

# Only the owner (the user running the services) may write
chmod 755 /opt/ocpp/data/SQLite
```

> **Note:** The services must be able to write into this **directory**, not only into the database file: SQLite runs in WAL mode and creates the files `OCPP.Core.sqlite-wal` and `OCPP.Core.sqlite-shm` next to the database. Use a local disk (SD card or SSD), not a network drive.

---

## Step 4: Configure OCPP.Core.Server

Open the server configuration file:

```bash
nano /opt/ocpp/server/appsettings.json
```

Adjust the following settings:

```json
{
  "ConnectionStrings": {
    "SQLite": "Filename=/opt/ocpp/data/SQLite/OCPP.Core.sqlite;"
  },

  "MessageDumpDir": "",
  "DbMessageLog": 0,
  "ValidateMessages": true,
  "AutoMigrateDB": true,

  "ApiKey": "YOUR-SECRET-API-KEY",

  "AllowedHosts": "*",

  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8081"
      }
    }
  }
}
```

> **Notes:**
> - `0.0.0.0` instead of `localhost` makes the service reachable from other devices on the network. If you use the Nginx reverse proxy (see below), use `http://127.0.0.1:8081` instead: then the unencrypted port is only reachable through the proxy.
> - **Remove the `HttpsInlineCertFile` endpoint** from the `Kestrel` section of the default `appsettings.json`. It uses the included development certificate `localhost.pfx` and is not needed on the Raspberry Pi (use the Nginx reverse proxy for HTTPS).
> - `AutoMigrateDB: true` automatically creates and migrates the database on first start.
> - Generate a long random `ApiKey`, e.g. with `openssl rand -hex 32`. Note it down — you will need the same value in the Management configuration.

---

## Step 5: Configure OCPP.Core.Management

```bash
nano /opt/ocpp/management/appsettings.json
```

```json
{
  "ConnectionStrings": {
    "SQLite": "Filename=/opt/ocpp/data/SQLite/OCPP.Core.sqlite;"
  },

  "ServerApiUrl": "http://127.0.0.1:8081/API",
  "ApiKey": "YOUR-SECRET-API-KEY",
  "AutoMigrateDB": true,

  "AllowedHosts": "*",

  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8082"
      }
    }
  }
}
```

> **Notes:**
> - `ApiKey` must be identical to the value in the server config.
> - `ServerApiUrl` uses `127.0.0.1` (not `localhost`): this also works when the server is bound to `127.0.0.1` only (`localhost` could first be resolved to the IPv6 address `::1`).
> - As for the server: remove the `HttpsInlineCertFile` endpoint, and use `http://127.0.0.1:8082` if you use the Nginx reverse proxy.
> - **Users:** don't configure users in `appsettings.json`. With an empty database the Management UI shows a setup page on the first visit, where you create the first administrator (passwords need at least 8 characters). Further users are managed in the UI ("Administration" → "Users"). Users from a `Users` section in the configuration are only taken over into the database once (as long as the database contains no users) - the plaintext passwords would remain in the file and later changes there have no effect.

---

## Step 6: Set Up systemd Services (Autostart)

### Service for OCPP.Core.Server

```bash
sudo nano /etc/systemd/system/ocpp-server.service
```

Content:

```ini
[Unit]
Description=OCPP.Core Server
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/opt/ocpp/server
ExecStart=/home/pi/.dotnet/dotnet /opt/ocpp/server/OCPP.Core.Server.dll
Restart=always
RestartSec=5
Environment=DOTNET_ROOT=/home/pi/.dotnet
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

### Service for OCPP.Core.Management

```bash
sudo nano /etc/systemd/system/ocpp-management.service
```

Content:

```ini
[Unit]
Description=OCPP.Core Management UI
After=network.target ocpp-server.service

[Service]
Type=simple
User=pi
WorkingDirectory=/opt/ocpp/management
ExecStart=/home/pi/.dotnet/dotnet /opt/ocpp/management/OCPP.Core.Management.dll
Restart=always
RestartSec=5
Environment=DOTNET_ROOT=/home/pi/.dotnet
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

> **Note:** Replace `pi` with your actual username if different (newer Raspberry Pi OS installations may use a different default user). It must be the user who installed .NET in step 1 and who owns `/opt/ocpp`.

### Enable and Start the Services

```bash
sudo systemctl daemon-reload

# Enable services to start on boot
sudo systemctl enable ocpp-server.service
sudo systemctl enable ocpp-management.service

# Start services now
sudo systemctl start ocpp-server.service
sudo systemctl start ocpp-management.service
```

### Check Status

```bash
sudo systemctl status ocpp-server.service
sudo systemctl status ocpp-management.service

# View live logs
sudo journalctl -u ocpp-server.service -f
sudo journalctl -u ocpp-management.service -f
```

---

## Step 7: Open Firewall Ports (optional)

If `ufw` is active and you **don't** use the Nginx reverse proxy:

```bash
sudo ufw allow 8081/tcp   # OCPP WebSocket server
sudo ufw allow 8082/tcp   # Management Web UI
```

With the Nginx reverse proxy only open HTTP/HTTPS (the ports 8081/8082 stay closed):

```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

---

## Verify the Installation

Find the Raspberry Pi's IP address:

```bash
hostname -I
```

Then open the following in a browser on another device on the network:

| Service | URL |
|---|---|
| Management Web UI | `http://<raspi-ip>:8082` |
| OCPP Server (WebSocket) | `ws://<raspi-ip>:8081/OCPP/<chargepoint-id>` |

On the first visit the Web UI shows the setup page: create the first administrator there and log in with it.

The server REST API (`/API/...`) is meant for the Management UI. Without `ApiKey` header it only accepts requests from the Raspberry Pi itself, e.g. `curl -H "X-API-Key: YOUR-SECRET-API-KEY" http://localhost:8081/API/Status`.

---

## Charge Point Security

If your charge points connect over the internet (or an untrusted network):

- Configure a username and password for every charge point in the Management UI (charge point details, the dice button generates a random password) and enter them in the charge point's OCPP settings (basic authentication).
- Set `"RequireChargePointAuthentication": true` in the server's `appsettings.json`: then the server rejects charge points without configured credentials.
- Use TLS (`wss://`) via the Nginx reverse proxy below - otherwise the passwords are transmitted unencrypted.

---

## Optional: Nginx as Reverse Proxy (HTTPS)

For production use, a reverse proxy with an SSL certificate (e.g. via Let's Encrypt) is strongly recommended. Bind both services to `127.0.0.1` in this case (see steps 4 and 5).

```bash
sudo apt-get install -y nginx certbot python3-certbot-nginx
```

Create an Nginx configuration for the OCPP server (`/etc/nginx/sites-available/ocpp`):

```nginx
server {
    listen 80;
    server_name your-domain.example.com;

    # The REST API is only for the Management UI (which uses localhost:8081 directly)
    # => never reachable through the proxy
    location /API {
        deny all;
    }

    # OCPP WebSocket (charge points connect to wss://your-domain.example.com/OCPP/<chargepoint-id>)
    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "Upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        # the server recognizes requests through a proxy by this header
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_read_timeout 3600;
    }
}

server {
    listen 80;
    server_name mgmt.your-domain.example.com;

    # Management Web UI
    location / {
        proxy_pass http://127.0.0.1:8082;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/ocpp /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx

# Obtain SSL certificate
sudo certbot --nginx -d your-domain.example.com -d mgmt.your-domain.example.com
```

---

## Backup

The data is in `/opt/ocpp/data/SQLite`. Because of the WAL mode the database consists of up to three files (`OCPP.Core.sqlite`, `-wal`, `-shm`) - copying only `OCPP.Core.sqlite` while the services are running can miss the latest changes.

- **While running** (recommended): `sudo apt-get install -y sqlite3`, then
  ```bash
  sqlite3 /opt/ocpp/data/SQLite/OCPP.Core.sqlite ".backup '/path/to/backup/OCPP.Core-$(date +%F).sqlite'"
  ```
- **Or** stop both services and copy all files of the directory:
  ```bash
  sudo systemctl stop ocpp-management.service ocpp-server.service
  cp /opt/ocpp/data/SQLite/OCPP.Core.sqlite* /path/to/backup/
  sudo systemctl start ocpp-server.service ocpp-management.service
  ```

Also back up both `appsettings.json` files.

---

## Service Management

```bash
# Restart after a configuration change
sudo systemctl restart ocpp-server.service
sudo systemctl restart ocpp-management.service

# Stop a service
sudo systemctl stop ocpp-server.service

# Follow live logs
sudo journalctl -u ocpp-server.service -f
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `dotnet: command not found` | Run `source ~/.bashrc` or `export PATH=$PATH:$HOME/.dotnet` |
| Service fails to start | Check logs: `journalctl -u ocpp-server.service -n 50` |
| `curl` download returns "Not Found" | Check the version on the release page and the `VERSION` variable (step 2) |
| SQLite permission error | Check permissions: `ls -la /opt/ocpp/data/SQLite/` - the service user needs write access to the directory (WAL files) |
| Cannot reach from network | Verify `0.0.0.0` is set in `appsettings.json` (or use the Nginx proxy) and check firewall rules |
| WebSocket disconnects via Nginx | Increase `proxy_read_timeout` to at least `3600` |
| Database not created | Ensure `AutoMigrateDB: true` in both configurations and the service user has write access to the SQLite directory |
| Management UI shows no setup page / login fails | The database already contains users (e.g. taken over from a `Users` section of the configuration) - log in with them |
