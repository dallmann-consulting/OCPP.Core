# Deploy naar de Raspberry Pi (srvrasp05)

Werkt zoals bij HomeControl en Huishoudboek. GitHub Actions bouwt bij elke push naar `main` self-contained
`linux-arm`-versies (workflow `build-pi.yml`, artefacten `ocpp-server` en `ocpp-mgmt`). De Pi haalt die zelf op.

`ocpp-deploy.timer` start elke minuut `ocpp-deploy.sh`. Dat script doet het volgende:

1. Het zoekt via de GitHub-API de laatste geslaagde `build-pi.yml`-run op `main`.
2. Het stopt als die commit al gedeployed is, of als die eerder faalde.
3. Het downloadt beide artefacten en bewaart een kopie van de huidige versie en van de database.
4. Het stopt `OcppManagement` en `OcppServer`, kopieert de nieuwe bestanden en start beide weer.
5. Het controleert of beide antwoorden. Zo niet, dan zet het de vorige versie en de database terug.

De Server migreert de database zelf bij het opstarten (`AutoMigrateDB`).

## Mappen en poorten

| Pad / poort | Inhoud |
|---|---|
| `/home/frederic/OCPP/server` | OCPP.Core.Server en `appsettings.Production.json` (niet in git) |
| `/home/frederic/OCPP/mgmt` | OCPP.Core.Management en `appsettings.Production.json` (niet in git) |
| `/mnt/data/ocpp` | database `OCPP.Core.sqlite`, Data Protection-sleutels in `keys/` en back-ups. **Staat op de USB-stick, niet op de SD-kaart.** |
| `0.0.0.0:8081` | Server: OCPP-websocket `/OCPP` voor de Alfen en `/API` voor Management (met `ApiKey`) |
| `127.0.0.1:8082` | Management. Vanuit de tailnet via `https://srvrasp05.tail010d5a.ts.net:8445` |

## Installatie op de Pi

Voer dit uit vanuit een checkout of kopie van de repo op de Pi, of kopieer eerst `deploy/` en
`SQLite/OCPP.Core.Empty.sqlite` met `scp`.

```bash
# Datamap op de USB-stick
sudo mkdir -p /mnt/data/ocpp/keys && sudo chown -R frederic: /mnt/data/ocpp
chmod 700 /mnt/data/ocpp

# Startdatabase. De Server kan geen lege database aanmaken: de EF-migraties gaan uit van het
# bestaande schema. Bij de echte overstap komt hier de gemigreerde Azure-database.
cp SQLite/OCPP.Core.Empty.sqlite /mnt/data/ocpp/OCPP.Core.sqlite

# App-mappen en instellingen. Vul de VUL-IN-waarden in en gebruik in beide bestanden dezelfde nieuwe ApiKey.
mkdir -p ~/OCPP/server ~/OCPP/mgmt
install -m 600 deploy/appsettings.Server.Production.example.json ~/OCPP/server/appsettings.Production.json
install -m 600 deploy/appsettings.Management.Production.example.json ~/OCPP/mgmt/appsettings.Production.json
nano ~/OCPP/server/appsettings.Production.json
nano ~/OCPP/mgmt/appsettings.Production.json

# App-services
sudo cp deploy/OcppServer.service deploy/OcppManagement.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable OcppServer OcppManagement

# Deploy-script
mkdir -p ~/ocpp-deploy
install -m 755 deploy/ocpp-deploy.sh deploy/ocpp-backup.sh ~/ocpp-deploy/

# GitHub-token: fine-grained, alleen binkycode/OCPP.Core, permissie "Actions: Read-only"
install -m 600 /dev/null ~/.config/ocpp-deploy.env
nano ~/.config/ocpp-deploy.env        # GITHUB_TOKEN=github_pat_...

# Droge run: downloadt en controleert de laatste build, deployt niets
DRY_RUN=1 ~/ocpp-deploy/ocpp-deploy.sh

# Timers installeren
sudo cp deploy/ocpp-deploy.service deploy/ocpp-deploy.timer deploy/ocpp-backup.service deploy/ocpp-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now ocpp-deploy.timer ocpp-backup.timer

# Management bereikbaar maken in de tailnet
sudo tailscale serve --bg --https=8445 http://127.0.0.1:8082
```

Het deploy-script herstart de apps met `sudo -n`. Daarvoor is deze regel nodig
(`sudo visudo -f /etc/sudoers.d/ocpp`):

```
frederic ALL=(root) NOPASSWD: /usr/bin/systemctl stop OcppServer, /usr/bin/systemctl start OcppServer, /usr/bin/systemctl stop OcppManagement, /usr/bin/systemctl start OcppManagement
```

## Laadpaal

Pas de laadpaal pas aan bij de overstap (zie `docs/MIGRATIE-PI.md` §6). Dat gebeurt in ACE Service Installer →
Connectivity → Network Profile 1 → CSMS URL `ws://192.168.0.160:8081/OCPP`, met Security Profile 0.

## Back-ups

Elke nacht rond 03:45 maakt `ocpp-backup.sh` een kopie via de SQLite backup-API en controleert die met
`PRAGMA integrity_check`. De kopie wordt gecomprimeerd bewaard op de USB-stick (`/mnt/data/ocpp/backups`) en op
de SD-kaart (`~/ocpp-backups`): 14 dagelijkse en 12 maandelijkse kopieën.

Daarnaast gaat er een met age versleutelde kopie naar OneDrive (`hbonedrive:OCPP-backups`, 30 dagelijkse en
12 maandelijkse). Die gebruikt dezelfde rclone-remote en publieke sleutel als Huishoudboek.

Terugzetten:

```bash
sudo systemctl stop OcppManagement OcppServer
zcat ~/ocpp-backups/daily-JJJJ-MM-DD.sqlite.gz > /mnt/data/ocpp/OCPP.Core.sqlite
sudo systemctl start OcppServer OcppManagement
```

## Opvolgen

```bash
journalctl -u ocpp-deploy -f                 # deploy-log
journalctl -u OcppServer -f                  # OCPP-verkeer met de laadpaal
journalctl -u OcppManagement -f              # web-UI en Hangfire
systemctl list-timers 'ocpp-*'               # volgende deploy-controle en back-up
```

Een build die faalde, wordt niet opnieuw geprobeerd. De volgende commit op `main` wel.
Opnieuw proberen: `rm ~/.local/state/ocpp-deploy/failed-sha`.
