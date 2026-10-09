# Design

## Route boundary

Vue owns only the Phase 2 settings routes:

- `/system/access`
- `/system/payment`
- `/system/youtube-login`
- `/system/audit`
- `/system/backups`
- `/system/obs-layouts`
- `/system/obs-assets`
- `/system/sound-cues`
- `/system/visual-styles`

The router guard reads the existing session token from session storage. The API client sends it only in `X-EasyLottery-Session-Token`; no token is logged or put in URLs.

## API reuse

- `GET/PUT /api/settings/payments`
- `GET/PUT /api/settings/obs-layout`
- `GET/PUT /api/settings/sound-cues`
- `GET/PUT /api/settings/visual-style`
- `GET /api/settings/backups`, `POST /api/settings/backups/{id}/restore`
- existing Passkey admin endpoints
- existing OBS asset endpoints

Audit uses the existing compatibility settings endpoint to preserve the current server-side audit behavior; no new backend contract is introduced in this phase.

## Coexistence

The ASP.NET host continues serving the existing Blazor fallback for every route outside the Phase 1 and Phase 2 Vue route set. The Vue admin layout is intentionally separate from the public layout so non-admin pages cannot accidentally initialize authenticated settings.

## Error handling

The shared client preserves `401`, `403`, `409`, and validation errors as typed `ApiError` values. Settings saves send the last received ETag and replace the local value with the server response after success.
