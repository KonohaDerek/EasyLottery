CREATE TABLE streamers (
    id uuid PRIMARY KEY,
    slug varchar(80) NOT NULL UNIQUE,
    display_name varchar(120) NOT NULL,
    status varchar(24) NOT NULL CHECK (status IN ('active', 'disabled', 'pending_deletion', 'deleted')),
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    pending_deletion_at_utc timestamptz NULL,
    deleted_at_utc timestamptz NULL
);

CREATE TABLE streamer_sites (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES streamers(id),
    name varchar(120) NOT NULL,
    base_url text NOT NULL,
    status varchar(24) NOT NULL CHECK (status IN ('active', 'disabled', 'revoked')),
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, base_url)
);

CREATE TABLE site_credentials (
    id uuid PRIMARY KEY,
    site_id uuid NOT NULL REFERENCES streamer_sites(id),
    client_id varchar(64) NOT NULL UNIQUE,
    secret_hash bytea NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NULL,
    revoked_at_utc timestamptz NULL
);
CREATE INDEX ix_site_credentials_site_live ON site_credentials(site_id, created_at_utc DESC) WHERE revoked_at_utc IS NULL;
CREATE UNIQUE INDEX ux_site_credentials_one_current ON site_credentials(site_id) WHERE expires_at_utc IS NULL AND revoked_at_utc IS NULL;

CREATE TABLE streamer_invites (
    id uuid PRIMARY KEY,
    streamer_slug varchar(80) NOT NULL,
    streamer_display_name varchar(120) NOT NULL,
    recipient_email_hash bytea NOT NULL,
    invite_token_hash bytea NOT NULL UNIQUE,
    expires_at_utc timestamptz NOT NULL,
    redeemed_at_utc timestamptz NULL,
    registration_options_json text NULL,
    registration_options_expires_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    created_by varchar(120) NOT NULL
);

CREATE TABLE streamer_accounts (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL UNIQUE REFERENCES streamers(id),
    email_hash bytea NOT NULL UNIQUE,
    passkey_credential_id bytea NOT NULL UNIQUE,
    passkey_public_key bytea NOT NULL,
    passkey_user_handle bytea NOT NULL,
    passkey_signature_count bigint NOT NULL DEFAULT 0,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE streamer_passkey_login_challenges (
    id uuid PRIMARY KEY,
    options_json text NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    consumed_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE email_delivery_outbox (
    id uuid PRIMARY KEY,
    aggregate_id uuid NOT NULL REFERENCES streamer_invites(id),
    message_type varchar(80) NOT NULL,
    protected_payload bytea NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    available_at_utc timestamptz NOT NULL DEFAULT now(),
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    last_attempt_at_utc timestamptz NULL,
    delivered_at_utc timestamptz NULL,
    last_error_code varchar(80) NULL
);
CREATE INDEX ix_email_delivery_outbox_pending ON email_delivery_outbox(available_at_utc) WHERE delivered_at_utc IS NULL;

CREATE TABLE viewers (
    id uuid PRIMARY KEY,
    email_hash bytea NOT NULL UNIQUE,
    handle varchar(32) NULL UNIQUE,
    terms_version_accepted varchar(40) NOT NULL,
    terms_accepted_at_utc timestamptz NOT NULL,
    age_bracket varchar(12) NOT NULL CHECK (age_bracket IN ('13_17', '18_plus')),
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    deleted_at_utc timestamptz NULL
);

CREATE TABLE viewer_login_codes (
    id uuid PRIMARY KEY,
    email_hash bytea NOT NULL,
    code_hash bytea NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    requested_at_utc timestamptz NOT NULL DEFAULT now(),
    consumed_at_utc timestamptz NULL
);
CREATE INDEX ix_viewer_login_codes_email_requested ON viewer_login_codes(email_hash, requested_at_utc DESC);

CREATE TABLE viewer_groups (
    id uuid PRIMARY KEY,
    viewer_id uuid NOT NULL REFERENCES viewers(id),
    streamer_id uuid NULL REFERENCES streamers(id),
    name varchar(80) NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    UNIQUE (viewer_id, streamer_id, name)
);
CREATE UNIQUE INDEX ux_viewer_groups_global_name ON viewer_groups(viewer_id, lower(name)) WHERE streamer_id IS NULL;

CREATE TABLE viewer_group_items (
    group_id uuid NOT NULL REFERENCES viewer_groups(id) ON DELETE CASCADE,
    card_instance_id uuid NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (group_id, card_instance_id)
);

CREATE TABLE audit_events (
    id uuid PRIMARY KEY,
    tenant_id uuid NULL REFERENCES streamers(id),
    actor_type varchar(24) NOT NULL,
    actor_id varchar(120) NOT NULL,
    event_type varchar(80) NOT NULL,
    subject_type varchar(80) NOT NULL,
    subject_id varchar(120) NOT NULL,
    occurred_at_utc timestamptz NOT NULL DEFAULT now(),
    payload jsonb NOT NULL DEFAULT '{}'::jsonb
);
CREATE INDEX ix_audit_events_tenant_occurred ON audit_events(tenant_id, occurred_at_utc DESC);
