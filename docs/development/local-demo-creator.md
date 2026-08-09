# Local Demo Creator

The repository includes a permanent development-only Creator named **Aegean
Field Notes**. It exercises the same Creator, Content, Address, QR, and
Presentation Engine paths as the flagship site while using separate content and
branding.

## Start Both Local Creators

Run the HTTP development profile:

```bash
dotnet run \
  --project src/TheSimontonAdventures.Web/TheSimontonAdventures.Web.csproj \
  --launch-profile http
```

Then open:

- Flagship: `http://localhost:5018`
- Demo Creator: `http://demo.localhost:5018`

The `.localhost` top-level domain resolves to the local loopback interface in
modern browsers, so this normally requires no hosts-file change.

## Isolation Checks

Both Creators own the public slug `athens`:

- `http://localhost:5018/go/athens` redirects to the flagship Mediterranean
  destination.
- `http://demo.localhost:5018/go/athens` redirects to the demo Aegean Notebook
  destination.

The demo header, metadata, colors, typography, current volume, destination, and
feature navigation are intentionally distinct. Adventures Companion is disabled
for the demo Creator.

## Production Safety

The demo manifest has `developmentOnly` set to `true`. Development aliases are
read only in the Development environment, and development-only Creators are
rejected by registered-domain resolution outside Development. Unknown production
hosts continue to receive `421 Misdirected Request`.
