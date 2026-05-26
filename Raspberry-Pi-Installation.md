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
sudo apt-get install -y curl libicu-dev

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

### Option A: Release ZIP (recommended)

Download the latest release from GitHub:

```bash
# Create directory
sudo mkdir -p /opt/ocpp
sudo chown $USER:$USER /opt/ocpp
cd /opt/ocpp

# Download the latest release (adjust URL if needed)
curl -L https://github.com/dallmann-consulting/OCPP.Core/releases/latest/download/OCPP.Core.zip -o ocpp.zip
unzip ocpp.zip -d .
rm ocpp.zip
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

# Set write permissions
chmod 755 /opt/ocpp/data/SQLite
```

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

  "MessageDumpDir": "/tmp/ocpp",
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
> - `0.0.0.0` instead of `localhost` makes the service reachable from other devices on the network.
> - `AutoMigrateDB: true` automatically creates and migrates the database on first start.
> - Note down the `ApiKey` — you will need the same value in the Management configuration.

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

  "ServerApiUrl": "http://localhost:8081/API",
  "ApiKey": "YOUR-SECRET-API-KEY",

  "Users": [
    {
      "Username": "admin",
      "Password": "secure-password",
      "Administrator": true
    },
    {
      "Username": "user",
      "Password": "another-password",
      "Administrator": false
    }
  ],

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

> **Important:** `ApiKey` must be identical to the value in the server config. The `Users` list defines all Web UI users with plain-text passwords.

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

> **Note:** Replace `pi` with your actual username if different (newer Raspberry Pi OS installations may use a different default user).

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

If `ufw` is active:

```bash
sudo ufw allow 8081/tcp   # OCPP WebSocket server
sudo ufw allow 8082/tcp   # Management Web UI
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
| Server REST API | `http://<raspi-ip>:8081/API/Status` |

Log in to the Web UI with the admin credentials you configured in `appsettings.json`.

---

## Optional: Nginx as Reverse Proxy (HTTPS)

For production use, a reverse proxy with an SSL certificate (e.g. via Let's Encrypt) is strongly recommended:

```bash
sudo apt-get install -y nginx certbot python3-certbot-nginx
```

Create an Nginx configuration for the OCPP server (`/etc/nginx/sites-available/ocpp`):

```nginx
server {
    listen 80;
    server_name your-domain.example.com;

    # OCPP WebSocket and API
    location / {
        proxy_pass http://localhost:8081;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "Upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_read_timeout 3600;
    }
}

server {
    listen 80;
    server_name mgmt.your-domain.example.com;

    # Management Web UI
    location / {
        proxy_pass http://localhost:8082;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
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
| SQLite permission error | Check permissions: `ls -la /opt/ocpp/data/SQLite/` |
| Cannot reach from network | Verify `0.0.0.0` is set in `appsettings.json` and check firewall rules |
| WebSocket disconnects via Nginx | Increase `proxy_read_timeout` to at least `3600` |
| Database not created | Ensure `AutoMigrateDB: true` in server config and server has write access to the SQLite directory |
