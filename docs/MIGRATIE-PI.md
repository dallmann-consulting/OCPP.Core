# Migratieplan: OCPP.Core van Azure naar de Raspberry Pi (srvrasp05)

Opgesteld 2026-11-11. Doel: de OCPP-server en de Management-webapp, die nu op Azure draaien
(resourcegroepen `LaadpaalZwevegem` en `lsoft-ocpp-svr_group`, webapps `lsoft-ocpp-svr` en
`lsoft-ocpp-mgt`, plus Azure SQL), op de Pi laten draaien, met dezelfde aanpak als
HomeControl, Huishoudboek en FienFit.

## 1. Uitgangssituatie

**Pi (srvrasp05, 192.168.0.160)**: Pi 5, 4 GB RAM, 32-bit userland (armhf), tijdzone Europe/Brussels.
Er is ongeveer 2,9 GB RAM vrij, de belasting is ongeveer 0,1, er is 20 GB vrij op de SD-kaart en 14 GB op
de USB-stick (`/mnt/data`). De poorten 8081, 8082 en 8445 zijn vrij. Andere apps gebruiken de poorten
5080, 5081, 5100 en 5443, en via Tailscale serve de poorten 443, 8443 en 8444.

**Laadpaal**: Alfen Eve Single Pro-line (`ace0334844`), 192.168.0.241, bedraad, firmware 7.4.5,
OCPP 1.6. Hij gebruikt Security Profile 0 en de CSMS URL `ws://lsoft-ocpp-svr.azurewebsites.net/OCPP`
in Network Profile 1. Laadpaal en Pi zitten op hetzelfde LAN.

**Code (fork van upstream OCPP.Core, nu .NET 8, wordt .NET 10)**:
- `OCPP.Core.Server`: websocket-endpoint `/OCPP` en een API die Management gebruikt (`ServerApiUrl`, `ApiKey`).
- `OCPP.Core.Management`: web-UI. In de fork zijn daar Hangfire, een Excel-export per kwartaal via e-mail
  (`EmailExcelExportService`, ClosedXML) en `CregPriceService` aan toegevoegd.
- De database kan al zowel SQL Server als SQLite gebruiken (`DbContextExtensions`). Er zit geen ruwe SQL in de fork.
- **Wat SQL Server nodig heeft**: `Hangfire.SqlServer` (`Management/Startup.cs`, `UseSqlServerStorage`).
- De CI bouwt op `windows-latest` en deployt via een publish profile naar Azure. Dat moet worden vervangen.

**Azure (bekeken 2026-11-11, abonnement LSoft-DEV, tenant "Standaardmap" met MFA, dus aanmelden met
`az login --tenant 8a411eba-397d-4120-b9b2-9a9923de60dd`)**:

| Resource | Groep | Details |
|---|---|---|
| Webapp `lsoft-ocpp-svr` | lsoft-ocpp-svr_group | Windows, .NET 8, websockets aan, httpsOnly uit, AlwaysOn uit. Draait op plan `ASP-LaadpaalZwevegem-b24a` (**F1 Free**) |
| Webapp `lsoft-ocpp-mgt` | LaadpaalZwevegem | Windows, .NET 8, httpsOnly aan, AlwaysOn uit. Draait op plan `ASP-lsoftocppsvrgroup-9b39` (**F1 Free**). De plannen zitten dus kruiselings in elkaars groep |
| SQL-server `lsoft-ocpp-sql` / db `lsoft-ocpp-core-db` | LaadpaalZwevegem | **Basic** (5 DTU, max 2 GB, geo-redundante back-up), **~118 MB in gebruik**. Firewall: "Allow Azure services" en één oud client-IP uit 2023 |
| Application Insights `lsoft-ocpp-svr` en `lsoft-ocpp-mgt` | beide | met Smart Detection-alertregels en een action group |
| Managed identity `lsoft-ocpp-mgt-id-9552` | LaadpaalZwevegem | waarschijnlijk voor de GitHub-deploy aangemaakt |

Kosten: Azure geeft voor dit abonnement geen bedragen per resource terug. Op basis van de lijstprijzen
kost alleen de SQL Basic-database iets, ongeveer €4–5 per maand. De App Service-plannen zijn gratis (F1),
en Application Insights blijft vermoedelijk binnen de gratis hoeveelheid.

**App settings op Azure** die naar `appsettings.Production.json` op de Pi moeten. Neem de waarden over uit
de portal of met `az webapp config appsettings list`, maar zet ze niet in de repo:
- **Server**:
  - `ApiKey`
  - connection string `SqlServer`, alleen nodig voor de datamigratie
- **Management**:
  - `ApiKey` en `ServerApiUrl`
  - `SmtpSettings:Password`
  - `Users:0:UserName`, `Users:0:Password` en `Users:0:Administrator`
  - `Users:1:Password`. De gebruikersnaam blijft `user` uit de repo.
  - connection strings `SqlServer` en `HangfireDb`
- **De `APPLICATIONINSIGHTS_*`- en `XDT_*`-instellingen** zijn alleen voor Azure en verhuizen niet mee.

## 2. Doelarchitectuur

```
Alfen ──ws://192.168.0.160:8081/OCPP──► OCPP.Core.Server  (LAN, 0.0.0.0:8081)
                                               │  SQLite /mnt/data/ocpp/OCPP.Core.sqlite
                                               ▲
                     http://127.0.0.1:8081/API │
jij (Tailscale) ──https://srvrasp05…:8445──► OCPP.Core.Management (127.0.0.1:8082)
                                                  Hangfire-storage: SQLite /mnt/data/ocpp/hangfire.sqlite
```

- Beide apps worden **self-contained `linux-arm`** gebouwd op GitHub (`ubuntu-latest`), net als Huishoudboek.
  Op de Pi is dan geen .NET-runtime nodig.
- Deploy gebeurt via een **pull-timer op de Pi**. GitHub ondersteunt geen ARM32-runners, dus dezelfde
  aanpak als `huishoudboek-deploy` met health-check en rollback.
- **Server**: luistert op het LAN, want de laadpaal moet erbij kunnen. Alleen `/OCPP` en `/API` worden daar
  aangeboden, en `/API` is beveiligd met de `ApiKey`.
- **Management**: alleen lokaal bereikbaar en naar buiten via `tailscale serve --https=8445`, alleen binnen de tailnet.
- **Data en back-ups** staan op de USB-stick, met een nachtelijke back-up zoals bij Huishoudboek.

## 3. Codewijzigingen (in een branch, bijvoorbeeld `feature/pi-hosting`)

0. **Upgrade naar .NET 10** (als eerste, aparte commit)
   - Alle projecten in `OCPP.Core.sln` krijgen `net10.0`, ook `Extensions/*` en `Simulators`, voor zover die
     in de solution zitten.
   - `Microsoft.EntityFrameworkCore.*` naar 10.x en de overige pakketten naar hun laatste stabiele versie
     (Hangfire 1.8.x, ClosedXML, CsvHelper, Karambolo.Extensions.Logging.File).
   - EF Core 10 ziet het model mogelijk iets anders dan de bestaande snapshot. Controleer met
     `dotnet ef migrations add Net10Check` dat de migratie leeg is, en gooi ze daarna weer weg.
   - .NET 10 draait al op deze Pi (Huishoudboek, self-contained `linux-arm`), dus voor armhf is dat bewezen.
   - Dezelfde stap doen voor upstream-code is niet nodig: de fork wordt niet meer gesynchroniseerd binnen
     deze migratie.

1. **Hangfire-storage configureerbaar maken**
   - Pakket `Hangfire.Storage.SQLite` toevoegen. Geef de voorkeur aan `Hangfire.InMemory` als uitvallen
     van de geplande job na een herstart aanvaardbaar is. Omdat de recurring job bij elke start opnieuw
     via `AddOrUpdate` wordt geregistreerd, volstaat in-memory eigenlijk. Een gemiste run tijdens een
     herstart op het exacte cron-moment is het enige risico.
   - In `Startup.cs` een storagekeuze maken: `SqlServer` als `HangfireDb` een SQL Server-string is,
     anders SQLite of InMemory. `Hangfire.SqlServer` kan dan blijven voor wie lokaal op Windows test.
2. **Geheimen uit de repo halen**
   - Het wachtwoord van het Hangfire-dashboard (`PasswordClear`) staat hardcoded in `Startup.cs`. Verplaats
     het naar configuratie en wijzig het, want het staat in de git-historie.
   - `ApiKey`, `SmtpSettings` en connection strings komen alleen in `appsettings.Production.json` op de Pi
     (untracked, net als bij HomeControl). Verander de `ApiKey` bij de overstap.
3. **Paden configureerbaar maken die nu Windows-defaults hebben**: `MessageDumpDir` (`c:\temp\OCPP`)
   leegmaken of op `/mnt/data/ocpp/dump` zetten, en de `Logging:File:BasePath` naar `/mnt/data/ocpp/logs`
   zetten of naar de journal laten gaan.
4. **Logvolume beperken**: zet `DbMessageLog` op `1`. Op `2` komt elke MeterValue van de Alfen in de
   database. Zet het OCPP-loglevel in productie op `Information` in plaats van `Trace`.
5. **ClosedXML op Linux**: de Excel-export eenmalig testen op de Pi. `AdjustToContents` heeft fonts nodig;
   er staan er 164 op de Pi, dus dat zal waarschijnlijk gewoon werken.
6. **CI**: een nieuwe workflow `build-pi.yml` op `ubuntu-latest` die beide projecten publiceert met
   `-r linux-arm --self-contained true` als twee artefacten (`ocpp-server`, `ocpp-mgmt`). De oude
   Azure-workflows uitschakelen of verwijderen pas na de overstap (zie stap 7).
7. **`deploy/`-map** naar het voorbeeld van Huishoudboek:
   - `OCPP.Server.service` (`ASPNETCORE_URLS=http://0.0.0.0:8081`, `RequiresMountsFor=/mnt/data`)
   - `OCPP.Management.service` (`ASPNETCORE_URLS=http://127.0.0.1:8082`)
   - `ocpp-deploy.sh`, `.service` en `.timer` (pull van de laatste geslaagde build, health-check en rollback)
   - `ocpp-backup.sh`, `.service` en `.timer` (SQLite `.backup` naar `/mnt/data/ocpp/backups` en `~/ocpp-backups`)
   - `appsettings.Production.example.json` voor beide apps
   - `README.md` met installatiestappen

### Status fase A (2026-11-11, branch `feature/pi-hosting`, gecommit)

Klaar en getest:
- **Upgrade naar .NET 10**: alle projecten gebruiken `net10.0`. EF Core en Microsoft.Extensions staan op 10.0.15,
  Newtonsoft.Json op 13.0.4 en Hangfire op 1.8.25.
  - Karambolo.Extensions.Logging.File blijft bewust op 3.6.3: 4.x is een major-upgrade met mogelijke
    wijzigingen in de configuratie.
  - **Wat EF Core 10 veranderde**: `Migrate()` gooit sinds EF 9 een fout bij een afwijkend model. Bij deze code
    is dat altijd zo, want de snapshot is voor SQL Server en mist de FK van ConnectorStatus naar ChargePoint
    (dat is al zo in upstream). Daarom wordt `PendingModelChangesWarning` genegeerd in `DbContextExtensions`.
    Dat is hetzelfde gedrag als EF 8. Er is bewust geen nieuwe migratie toegevoegd.
- **Hangfire**: SQL Server-storage als `ConnectionStrings:HangfireDb` gezet is, anders `Hangfire.InMemory`.
  Het dashboard wordt alleen aangeboden als `Hangfire:DashboardUser` en `Hangfire:DashboardPassword`
  geconfigureerd zijn. Het hardcoded wachtwoord is weg.
- **DataProtection**: het pad komt nu uit `DataProtectionKeysPath` in plaats van het hardcoded Azure-pad.
- **HTTPS-endpoint met localhost.pfx** verplaatst naar `appsettings.Development.json`, zodat productie alleen
  HTTP gebruikt, op de poort die de config opgeeft.
- **Azure-workflows** draaien niet meer automatisch bij een push, alleen nog via `workflow_dispatch`. Zo
  belandt de .NET 10-code niet per ongeluk op Azure.
- **Nieuwe workflow `build-pi.yml`**: publish als self-contained `linux-arm`, met artefacten `ocpp-server`
  en `ocpp-mgmt`. Geen single-file.

Lokale test (Windows, SQLite, in-memory Hangfire) met een gesimuleerde OCPP 1.6-sessie: BootNotification,
StatusNotification, Authorize, StartTransaction, MeterValues, StopTransaction en Heartbeat werden allemaal
aanvaard. De transactie stond in SQLite en was zichtbaar in de Management-UI. Inloggen en het
Hangfire-dashboard (401 zonder en 200 met credentials, recurring job aanwezig) werkten ook.

Rooktest op de Pi: de linux-arm-build van de Server start met SQLite, antwoordt op `/` en gebruikt ~95 MB RSS.

Nog niet getest: de Excel-export per mail (ClosedXML op Linux). Die zou echt een mail versturen, dus dat
gebeurt in fase C via het dashboard.

**Configuratie die op de Pi nodig is** (`appsettings.Production.json`, fase B):
- **Server**:
  - `ConnectionStrings:SqlServer` = `""`
  - `ConnectionStrings:SQLite` = `Filename=/mnt/data/ocpp/OCPP.Core.sqlite`
  - `Kestrel:Endpoints:Http:Url` = `http://0.0.0.0:8081`
  - `MessageDumpDir` = `""`
  - `DbMessageLog` = `1`
  - een nieuwe `ApiKey`
- **Management**:
  - dezelfde connection strings, plus `ConnectionStrings:HangfireDb` = `""`
  - `Kestrel:Endpoints:Http:Url` = `http://127.0.0.1:8082`
  - `ServerApiUrl` = `http://127.0.0.1:8081/API`
  - dezelfde nieuwe `ApiKey`
  - `DataProtectionKeysPath` = `/mnt/data/ocpp/keys`
  - `Hangfire:DashboardUser` en `Hangfire:DashboardPassword`
  - `SmtpSettings`
  - `Users` met echte wachtwoorden. De repo bevat nog de upstream-defaults `admin`/`t3st`.
- **Logbestanden** komen standaard in `Logs/` naast de app. Zet `Logging:File:BasePath` op `/mnt/data/ocpp/logs`.

## 4. Datamigratie Azure SQL → SQLite

Aanpak: een **eenmalige console-tool** (`tools/OCPP.Core.Migrate`, niet in de solution-build) die
`OCPPCoreContext` twee keer opent (SqlServer als bron, SQLite als doel) en de tabellen kopieert met
**behoud van de primaire sleutels**. Dat is belangrijk voor `TransactionId`, omdat de laadpaal naar
transactie-ID's verwijst.

- Volgorde: `ChargePoint` → `ChargeTags` → `ConnectorStatus` → `Transactions` → (optioneel) `MessageLog`.
- De hele database is maar ongeveer 118 MB, dus **alles inclusief `MessageLog` meenemen** is geen probleem
  voor de USB-stick.
- Het doel is een database die met `Migrate()` is aangemaakt (het `AutoMigrateDB`-pad van de Server), zodat
  `__EFMigrationsHistory` klopt. Daarna pas de data importeren.
- De Hangfire-data niet migreren: de recurring job registreert zichzelf opnieuw.
- Voorwaarden: tijdelijk een firewallregel op de Azure SQL-server voor je eigen IP, en de connection string
  uit de Azure-portal. Die zet je lokaal in user-secrets, niet in de repo.
- Controle na import: aantal rijen per tabel bron = doel, laatste `TransactionId`, en de som van `MeterStop`
  per laadpaal over het voorbije kwartaal moet gelijk zijn.

## 5. Uitrol in fases

| Fase | Wat | Laadpaal nog op Azure? |
|---|---|---|
| A | Codewijzigingen 3.0–3.6 lokaal testen met SQLite (Simulator in `Simulators/`) | ja |
| B | `build-pi.yml` en `deploy/` mergen, services en timers op de Pi installeren, met een **lege** SQLite-database | ja |

**Status fase B (2026-11-11):** de `deploy/`-map is klaar (services `OcppServer` en `OcppManagement`, `ocpp-deploy`- en `ocpp-backup`-timers, voorbeeldconfig per app en de installatiestappen in `deploy/README.md`). De voorbeeldconfig is lokaal getest: juiste poorten, alleen de nieuwe ApiKey wordt aanvaard, de standaard-logins zijn vervangen en er komen geen logbestanden. Nog te doen: mergen naar `main`, zodat er een build-artefact op `main` komt, en daarna de installatie op de Pi.

| C | Proefmigratie van de data naar de Pi, Management-UI via Tailscale nakijken, Excel-export eenmaal handmatig starten via het Hangfire-dashboard | ja |
| D | **Overstap** (zie §6) | nee |
| E | Een week parallel beschikbaar laten: Azure stopt met ontvangen, maar wordt nog niet verwijderd | nee |
| F | Azure opruimen (zie §7) | nee |

## 6. Overstap (fase D), ongeveer 30 minuten, als er niet geladen wordt

1. Controleren dat er geen actieve laadsessie is (Management-UI op Azure).
2. Azure-webapp `lsoft-ocpp-svr` **stoppen**. Zo komen er geen nieuwe gegevens meer binnen in Azure.
3. Definitieve datamigratie draaien (§4) en de controles uitvoeren.
4. `OCPP.Server.service` en `OCPP.Management.service` op de Pi (her)starten.
5. In **ACE Service Installer** → Connectivity → Network Profile 1 → CSMS URL:
   `ws://192.168.0.160:8081/OCPP` (Security Profile blijft 0) → **Save**.
   Noteer de oude URL om terug te kunnen.
6. Controleren: de laadpaal staat online in de Management-UI (BootNotification en Heartbeat in de logs),
   een RFID-badge aanbieden en een korte laadsessie starten en stoppen, waarna de transactie in de UI verschijnt.
7. Nieuwe `ApiKey` en Hangfire-wachtwoord actief, oude Azure-workflows uitgeschakeld.

**Terugdraaien:** CSMS URL terugzetten naar Azure, `lsoft-ocpp-svr` weer starten. Sessies die in de
tussentijd op de Pi zijn geregistreerd, moet je dan handmatig overzetten. Hou de periode tussen overstap
en een eventuele terugkeer dus kort.

## 7. Opruimen in Azure (fase F, na ongeveer een week zonder problemen)

1. Eindexport (`.bacpac`) van de Azure SQL-database bewaren, bijvoorbeeld op `/mnt/data/ocpp/archive` en ook
   off-site.
2. Beide resourcegroepen bevatten alleen OCPP-resources (zie §1). Je kan `LaadpaalZwevegem` en
   `lsoft-ocpp-svr_group` dus in hun geheel verwijderen. Geen Service Bus aanwezig.
3. GitHub-secrets `AZUREAPPSERVICE_PUBLISHPROFILE_*` verwijderen.

## 8. Openstaande punten en risico's

- **De Azure-database** (~118 MB, Basic) is bekeken, zie §1. Voor de migratie moet je tijdelijk een
  firewallregel toevoegen voor je huidige publieke IP. Verwijder de oude regel `ClientIp-2023-12-27_19-50-1`.
- **Is de GitHub-repo publiek?** Zo ja, dan zijn de `ApiKey` en het Hangfire-wachtwoord in de historie
  sowieso te beschouwen als gelekt. Daarom vervangen we ze (3.2).
- **Afhankelijkheid van de Pi**: als de Pi uitvalt, laadt de Alfen nog wel (afhankelijk van de
  autorisatie-instellingen voor offline gebruik), maar worden sessies pas geregistreerd als de verbinding
  terug is. Controleer in ACE de instellingen voor offline autorisatie en "Local Authorization List".
- **Geen off-site back-up** op de Pi, net als bij Huishoudboek. Overweeg dat voor alle apps samen op te lossen.
- **Firewall**: op de Pi lijkt er geen firewall actief. Poort 8081 is dan voor het hele LAN open. Dat is
  aanvaardbaar, want het is alleen OCPP plus een API met een sleutel, maar je kan het met nftables beperken
  tot 192.168.0.241.
- **Upstream**: de fork loopt achter op `upstream/main` (juli 2025). Synchroniseren hoort niet bij deze migratie.
