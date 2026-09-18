# Balkis Hassan

Volledige .NET 10-vervanging van de oude Joomla 1.5-website van de Iraakse dichteres en schrijfster بلقيس حميد حسن. De publieke website en het beheer zijn Arabisch en RTL. De implementatie gebruikt ASP.NET Core MVC, Entity Framework Core, PostgreSQL en ASP.NET Core Identity. Er is bewust geen Docker-configuratie.

## Huidige migratiestatus

De reproduceerbare importer heeft de lokale Joomla SQL-dump en documentroot verwerkt. De huidige PostgreSQL-database bevat:

| Onderdeel | Aantal |
|---|---:|
| Zichtbare inhoudscategorieën | 8 |
| Technische archiefcategorie | 1 |
| Contentitems | 515 |
| Externe links | 15 |
| Contactrecords | 1 |
| Mediabestanden | 63 |
| Permanente legacy-redirects | 522 |

De drie lokale MP3-bestanden, boek-PDF's, relevante afbeeldingen en covers zijn gekopieerd naar `wwwroot/uploads`. De automatische encodingcontrole vond geen reeksen `????` en geen Unicode replacement characters in de gemigreerde inhoud. Zie [migration-analysis.md](migration-analysis.md) en [migration-report.md](migration-report.md) voor de bronanalyse en het laatste resultaat.

## Vereisten voor lokaal gebruik

- .NET SDK 10.0.302 of een compatibele latere patch
- PostgreSQL met UTF-8-database
- PowerShell voor de gegeven Windows-commando's

De lokale database kan zonder container worden aangemaakt met `psql`:

```sql
CREATE ROLE balkishassan_dev WITH LOGIN PASSWORD 'kies-zelf-een-sterk-lokaal-wachtwoord';
CREATE DATABASE balkishassan_dev OWNER balkishassan_dev ENCODING 'UTF8';
```

Sla de connection string uitsluitend op in .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=balkishassan_dev;Username=balkishassan_dev;Password=JE_EIGEN_WACHTWOORD" --project src/BalkisHassan.Web/BalkisHassan.Web.csproj
```

De migratietool deelt hetzelfde `UserSecretsId`; de connection string hoeft dus niet te worden gekopieerd naar een configuratiebestand.

## Eerste lokale start

Voer vanuit de solutionmap uit:

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update --project src/BalkisHassan.Infrastructure --startup-project src/BalkisHassan.Web
dotnet run --project src/BalkisHassan.Web
```

De standaard ontwikkel-URL is `http://localhost:5204`. De health check staat op `/health`; het beheergedeelte staat op `/admin`.

Bij iedere start worden nog niet toegepaste EF Core-migraties veilig uitgevoerd. Maak een nieuwe migratie na een modelwijziging met:

```powershell
dotnet ef migrations add BeschrijvendeNaam --project src/BalkisHassan.Infrastructure --startup-project src/BalkisHassan.Web
```

## Beheeraccount instellen

Kies zelf een e-mailadres en sterk wachtwoord en sla beide uitsluitend op in User Secrets:

```powershell
dotnet user-secrets set "AdminBootstrap:Email" "jouw-adres@example.com" --project src/BalkisHassan.Web/BalkisHassan.Web.csproj
dotnet user-secrets set "AdminBootstrap:Password" "JE_EIGEN_STERKE_WACHTWOORD" --project src/BalkisHassan.Web/BalkisHassan.Web.csproj
```

Start de applicatie daarna eenmaal. Het account wordt alleen aangemaakt wanneer het e-mailadres nog niet bestaat. Identity verzorgt salted password hashing, een veilige cookie en lockout na herhaalde mislukte pogingen. Verwijder eventueel het bootstrap-wachtwoord na die eerste geslaagde start:

```powershell
dotnet user-secrets remove "AdminBootstrap:Password" --project src/BalkisHassan.Web/BalkisHassan.Web.csproj
```

## Joomla-import opnieuw uitvoeren

De importer leest de SQL-dump rechtstreeks als strikte UTF-8; MySQL of MariaDB is niet nodig. Standaard gebruikt hij de al aanwezige lokale bronpaden. Start hem vanuit de solutionmap:

```powershell
dotnet run --project tools/BalkisHassan.JoomlaMigration
```

Andere bronpaden kunnen expliciet worden meegegeven:

```powershell
dotnet run --project tools/BalkisHassan.JoomlaMigration -- --sql "D:\bron\export.sql" --source-root "D:\bron\public_html" --web-root "D:\doel\wwwroot"
```

De import is idempotent op basis van Joomla-ID's en stabiele legacy-paden. Oude Joomla-gebruikers en wachtwoordhashes worden niet geïmporteerd. De oude bron wordt uitsluitend gelezen en nooit gewijzigd.

## Testen en publiceren

```powershell
dotnet test BalkisHassan.sln --configuration Release
powershell -ExecutionPolicy Bypass -File deploy/publish.ps1
```

Het publicatiescript test eerst en schrijft daarna een framework-dependent Linux-build naar `artifacts/publish`. Gebruik `-Runtime linux-arm64` voor een ARM64-VPS.

## Ubuntu VPS zonder Docker

De beoogde opstelling is Caddy → ASP.NET Core op `127.0.0.1:5001` → PostgreSQL. Installeer op een ondersteunde Ubuntu-versie de .NET 10 SDK of runtime, PostgreSQL, Caddy en rsync via de officiële pakketbronnen.

Maak database en OS-gebruiker als beheerder:

```bash
sudo -u postgres createuser --pwprompt balkishassan
sudo -u postgres createdb --owner=balkishassan --encoding=UTF8 balkishassan
sudo useradd --system --home /var/lib/balkishassan --create-home --shell /usr/sbin/nologin balkishassan
sudo install -d -o root -g balkishassan -m 0750 /etc/balkishassan
```

Kopieer `deploy/balkishassan.env.example` naar `/etc/balkishassan/balkishassan.env`, vul daar de productie-connection-string en eenmalige admin-bootstrapwaarden in en beveilig het bestand:

```bash
sudo chown root:balkishassan /etc/balkishassan/balkishassan.env
sudo chmod 0640 /etc/balkishassan/balkishassan.env
```

Kopieer vervolgens de checkout naar de VPS en voer vanuit de repository `bash deploy/deploy.sh` uit. Het script bouwt en test, maakt een release onder `/opt/balkishassan/releases`, koppelt uploads aan `/var/lib/balkishassan/uploads` en herstart de service. Installeer de service en Caddy-configuratie eenmalig:

```bash
sudo cp deploy/balkishassan.service /etc/systemd/system/balkishassan.service
sudo cp deploy/Caddyfile /etc/caddy/Caddyfile
sudo systemctl daemon-reload
sudo systemctl enable --now balkishassan
sudo systemctl reload caddy
curl --fail http://127.0.0.1:5001/health
```

Verwijder na de eerste succesvolle beheerderslogin `AdminBootstrap__Password` uit het beveiligde productie-env-bestand en herstart de service. Het reeds gehashte Identity-account blijft bestaan.

Pas DNS pas aan nadat de lokale health check en een tijdelijke hostnaam succesvol zijn getest. De configuratie wijzigt niets aan de bestaande live Joomla-site.

## Back-ups

`deploy/backup.sh` maakt een PostgreSQL custom-format dump, een gecomprimeerd uploads-archief en SHA-256-controlesommen. PostgreSQL-authenticatie hoort op de VPS in `~/.pgpass` met bestandsmodus `0600`; een wachtwoord staat nooit in het script. Plan het script bijvoorbeeld dagelijks met een systemd timer en synchroniseer de resulterende bestanden versleuteld naar een tweede locatie. Lokale retentie is standaard 14 dagen.

Controleer regelmatig een herstel naar een aparte testdatabase:

```bash
createdb balkishassan_restore_test
pg_restore --clean --if-exists --no-owner --dbname=balkishassan_restore_test database-YYYYMMDDTHHMMSSZ.dump
```

Pak het uploads-archief eveneens eerst in een tijdelijke map uit en controleer aantallen en checksums voordat productie wordt vervangen.

## Nooit in Git opnemen

- `login.txt`, `.env` en `deploy/*.env`
- echte `appsettings.*.local.json`-bestanden
- User Secrets uit het gebruikersprofiel
- database-dumps en back-ups
- de productie-connection-string en alle database- of adminwachtwoorden
- `bin`, `obj`, `TestResults` en `artifacts`

Deze paden zijn opgenomen in `.gitignore`. Bestaande gemigreerde media onder `src/BalkisHassan.Web/wwwroot/uploads` horen wel bij de eerste website-release; nieuwe productie-uploads staan persistent buiten de release onder `/var/lib/balkishassan/uploads`.
