# EasyLottery Card Hub

Card Hub foundation service for streamer tenants and site credentials. This project is scoped to wave 1a; viewer accounts, catalog, draws, redemption, and trading are added by later waves.

## Configuration

Set these values through the deployment secret/configuration store:

- `ConnectionStrings__CardHub`: PostgreSQL connection string.
- `Authentication__SigningKey`: at least 32 random UTF-8 bytes; keep stable across restarts and instances.
- `Authentication__Issuer`: stable HTTPS issuer URL.
- `PlatformAdmin__BootstrapEmail`: initial platform administrator email.
- `PlatformAdmin__BootstrapToken`: at least 32 random bytes. Send it only in the `X-Platform-Admin-Key` header. The API seeds this email in `platform_admins`; rotate the deployment secret if it is exposed.
- `Passkey__RpId`, `Passkey__ServerName`, and `Passkey__Origins__0`: WebAuthn relying-party settings for the Hub's public origin.

`Database__RunMigrationsOnStartup` defaults to `true`; the service applies its additive EF migration before accepting requests. The first platform administrator is provisioned from configuration. The initial invite response includes the raw invite token exactly once; only its SHA-256 hash is stored. Deliver the token to the email-bound recipient through the operator's approved channel.

## Authentication boundaries

- Streamer Passkey sessions use audience `card-hub-streamer` and expire after 15 minutes.
- Site tokens use OAuth2 `client_credentials`, audience `card-hub-site`, and expire after 10 minutes. Site calls re-check the tenant lifecycle status and credential revocation/overlap on every request.
- `card-hub-viewer` is reserved in JWT validation for wave 1b; no viewer tables, tokens, or routes are implemented here.
- Site client secrets and invite tokens are never persisted in raw form. A rotated site secret overlaps for 24 hours; another rotation waits until that previous secret expires. Either credential can be revoked immediately.

Passkey ceremony state is held in process memory for wave 1 and expires after five minutes. It fails closed after restart; deployments with multiple replicas need sticky routing or a shared challenge store before horizontal scaling. Production startup also requires an HTTPS issuer and Passkey origins matching the configured public RP domain.

## Container

Build from the repository root with:

```sh
docker build -f src/EasyLotteryCardHub/Dockerfile -t easy-lottery-card-hub .
```

The image listens on port `18940`. PostgreSQL credentials and signing/bootstrap secrets must be provided at runtime.
