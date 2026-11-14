# EasyLottery Card Hub

Card Hub is a separate .NET 10 service. It stores its own PostgreSQL schema and issues tenant-scoped site tokens; it does not use the streamer's SQLite database.

## Local configuration

Set `ConnectionStrings__CardHub` to a local PostgreSQL database to apply the embedded schema migration. Without it, the process still exposes `/health/live`, while `/health/ready` remains unhealthy and data endpoints cannot run.

Before issuing site tokens, configure `CardHub__SiteToken__SigningKey` with base64 for at least 32 random bytes. Production also requires `CardHub__Identity__HmacKey` (base64, at least 32 random bytes) for email identity hashes and explicit `CardHub__Passkey__RpId` plus HTTPS-only `CardHub__Passkey__Origins`. Configure `CardHub__Admin__ApiKey` to enable management routes; send it in `X-CardHub-Admin-Key`. Configure `CardHub__EmailOutbox__EncryptionKey` (base64, exactly 32 random bytes) before creating invites. These values are secrets and must come from a local secret store or environment variables. Do not commit them.

Management routes return a site client secret only at creation or rotation. The database stores SHA-256 hashes. Rotation keeps the prior current secret valid for 24 hours, unless it is revoked sooner. Site access tokens use audience `card-hub-site` and expire after 15 minutes.

The service is not configured for a production provider or deployment by this change.

Streamer onboarding writes an encrypted email payload to the Postgres outbox and returns `queued`. No sender/worker is registered in this slice, so invites are not delivered. Delivery remains blocked until an email provider and authorized account are selected and configured. Enrollment requires a resident Passkey. Login starts without an email or credential allow-list at `/hub/v1/streamers/passkey/options`, then verifies at `/hub/v1/streamers/passkey/verify`; successful assertions return a fresh 15-minute token. This avoids exposing account existence or credential IDs in the options response. Tenant deletion requires entering `pending_deletion` and waiting at least 30 days. Successful site OAuth responses include top-level `tenant_id` and `tenant_name` metadata.
