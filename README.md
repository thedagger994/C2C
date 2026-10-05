# Coast 2 Coast Lead Engineering: website

Rebuild of https://www.c2cleadengineering.ca (previously on Wix) as a full-stack ASP.NET Core 10 site.

**Stack:** Razor Pages (server-rendered) · SQLite via EF Core · MailKit (email alerts) · cookie-auth admin inbox · Docker.

## Pages
| URL | Content |
|---|---|
| `/` | Hero, overview, services offered, quote CTA |
| `/services` | The six service categories |
| `/projects` | Project cards + job-site gallery |
| `/about-us` | Company, PEO registration, Managing Director |
| `/get-a-quote` | Quote form, contact details, "Work With Us" form with file upload |
| `/admin` | Password-protected inbox of quotes and applications (CV/profile downloads) |

## Run locally
```bash
cd C2C.Web
dotnet run
```
Open http://localhost:5038. In Development the admin login is `admin` / `dev-only-password`.

## Configuration (environment variables in production)
| Variable | Purpose |
|---|---|
| `Admin__Username`, `Admin__Password` | Admin login. **If the password is empty, the admin is disabled.** |
| `Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password`, `Smtp__From`, `Smtp__To` | Email alerts on new submissions (`To` may be comma-separated). Without a host, submissions are still saved and visible in `/admin`. |
| `DataDir` | Folder for the SQLite DB and uploaded files (mount as a volume; default `App_Data`). |

## Deploy
```bash
docker build -t c2c-website .
docker run -d -p 8080:8080 -v c2c-data:/data \
  -e Admin__Password='<strong password>' \
  -e Smtp__Host=smtp.example.com -e Smtp__User=... -e Smtp__Password=... -e Smtp__From=info@c2cleadengineering.ca \
  c2c-website
```
Put it behind an HTTPS reverse proxy / managed host (Azure App Service, Fly.io, Railway, Render, a VPS with Caddy). The app honours `X-Forwarded-*` headers.

### Pointing the GoDaddy domain at it
In GoDaddy → DNS for `c2cleadengineering.ca`: set the `www` CNAME (or A record) to the host's address, and forward the root domain to `www`. Then add the custom domain on your host to get a TLS certificate. Keep the Wix site live until the new one is verified, then cancel the Wix plan.

## Images
Originals are in `_raw/` (git-ignored). `wwwroot/img` holds responsive WebP copies (`name-480/960/1600.webp`) plus transparent logos. The `ImageCatalog` service builds `srcset` automatically from whatever files are in that folder.
