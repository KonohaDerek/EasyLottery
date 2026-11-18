# EasyLottery Card Hub

Card Hub foundation service for streamer tenants, site credentials, and viewer accounts. Catalog, draws, redemption, and trading are added by later waves.

## Configuration

Set these values through the deployment secret/configuration store:

- `ConnectionStrings__CardHub`: PostgreSQL connection string.
- `Authentication__SigningKey`: at least 32 random UTF-8 bytes; keep stable across restarts and instances.
- `Authentication__Issuer`: stable HTTPS issuer URL.
- `PlatformAdmin__BootstrapEmail`: initial platform administrator email.
- `PlatformAdmin__BootstrapToken`: at least 32 random bytes. Send it only in the `X-Platform-Admin-Key` header. The API seeds this email in `platform_admins`; rotate the deployment secret if it is exposed.
- `Passkey__RpId`, `Passkey__ServerName`, and `Passkey__Origins__0`: WebAuthn relying-party settings for the Hub's public origin.
- `Viewer__TermsVersion`: current terms/privacy acceptance version; defaults to `1` for development.

`Database__RunMigrationsOnStartup` defaults to `true`; the service applies its additive EF migration before accepting requests. The first platform administrator is provisioned from configuration. The initial invite response includes the raw invite token exactly once; only its SHA-256 hash is stored. Deliver the token to the email-bound recipient through the operator's approved channel.

## Authentication boundaries

- Streamer Passkey sessions use audience `card-hub-streamer` and expire after 15 minutes.
- Site tokens use OAuth2 `client_credentials`, audience `card-hub-site`, and expire after 10 minutes. Site calls re-check the tenant lifecycle status and credential revocation/overlap on every request.
- Viewer sessions use audience `card-hub-viewer`; access tokens expire after 15 minutes and 30-day refresh tokens rotate on use. Viewer tokens cannot authorize streamer, site, or platform-admin routes.
- Site client secrets and invite tokens are never persisted in raw form. A rotated site secret overlaps for 24 hours; another rotation waits until that previous secret expires. Either credential can be revoked immediately.

## Viewer accounts

- `POST /api/auth/viewer/code` accepts `{ "email": "...", "tenantId": "..." }`; `tenantId` is optional and selects the streamer-aware email copy. A successful request always returns the same `202 { "accepted": true }` response regardless of whether an account already exists. Resends are blocked for 60 seconds; the service also limits requests to five per email and 20 per IP per hour, in addition to the authentication endpoint's per-IP limit. These rate-limit buckets are in process memory, so multi-replica deployments need a shared limiter.
- `POST /api/auth/viewer/code/verify` accepts `email`, `loginCode`, `ageBracket`, `termsAccepted`, and `termsVersion`. New accounts require age bracket `13_17` or `18_plus` and acceptance of the configured terms version. Under-13 registration is rejected. Login codes expire after 10 minutes and allow five attempts.
- `POST /api/auth/viewer/refresh` accepts a refresh token and rotates it. Refresh tokens are stored as SHA-256 hashes; detected reuse revokes the token family.
- Authenticated viewer routes under `/api/viewer` provide custom group CRUD, synchronous JSON export (`Cache-Control: no-store`), and account deletion. The `viewer_group_items` table is ready for catalog/instance-backed membership. Deletion removes viewer email, groups, and sessions, writes an audit event, then calls the card-retirement interface. That interface is a no-op until card instances arrive in the draw wave.
- The only email sender included is a development/test logger. It will not log or send codes outside Development/Testing; production code delivery returns unavailable until a provider is selected under #346.

Passkey ceremony state is held in process memory for wave 1 and expires after five minutes. It fails closed after restart; deployments with multiple replicas need sticky routing or a shared challenge store before horizontal scaling. Production startup also requires an HTTPS issuer and Passkey origins matching the configured public RP domain.

## Container

Build from the repository root with:

```sh
docker build -f src/EasyLotteryCardHub/Dockerfile -t easy-lottery-card-hub .
```

The image listens on port `18940`. PostgreSQL credentials and signing/bootstrap secrets must be provided at runtime.
