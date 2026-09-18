# Migratieanalyse oude Joomla-website

## Bronnen

- SQL-dump: `C:\_tjip\Mohannad\website\balkisha-2.sql`
- Joomla-documentroot: `C:\_tjip\Mohannad\website\public_html`
- Joomla-versie: 1.5.14 (vastgesteld uit `libraries/joomla/version.php`)
- Publieke bestanden aangetroffen: 5155

## Database en encoding

De dump is een phpMyAdmin/MySQL-export met tabellen onder de prefix `jos_`. De tabellen gebruiken hoofdzakelijk
`utf8mb3_general_ci`. Het dumpbestand is zonder BOM opgeslagen en is als strikte UTF-8 valide. De importer leest
het bestand daarom rechtstreeks als UTF-8 en voert geen extra tekenconversie uit.

| Brontabel | Gelezen rijen |
|---|---:|
| `jos_categories` | 11 |
| `jos_contact_details` | 1 |
| `jos_content` | 512 |
| `jos_content_frontpage` | 2 |
| `jos_menu` | 13 |
| `jos_modules` | 21 |
| `jos_sections` | 8 |
| `jos_weblinks` | 15 |

## Migratiestrategie

De migratietool leest alleen `INSERT`-statements van relevante tabellen, decodeert MySQL-escapes, saneert oude HTML,
behoudt tekst en regeleinden, herschrijft lokale media-URL's en schrijft idempotent naar PostgreSQL op basis van
`LegacyJoomlaId`. Oude gebruikers en wachtwoordhashes worden bewust niet gemigreerd.

Media worden uitsluitend gekopieerd; de oude Joomla-directory blijft ongewijzigd. Bestandsnamen krijgen een veilige,
stabiele hash om botsingen te voorkomen. Scripts, events en onveilige embeds uit oude HTML worden verwijderd.

## Opvallende bevindingen

- De actieve documentroot is `public_html`; `httpdocs` is leeg.
- Drie lokale MP3-bestanden zijn gekoppeld vanuit de oude `mod_vplayer`-module.
- PDF's en boekcovers staan hoofdzakelijk direct onder `images` en `images/stories`.
- Hoofdmenu- en artikel-URL's krijgen permanente redirects.
- Het opnieuw uitvoeren van de importer actualiseert bestaande records en maakt geen duplicaten.