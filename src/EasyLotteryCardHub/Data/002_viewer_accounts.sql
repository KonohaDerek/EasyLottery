ALTER TABLE viewers DROP CONSTRAINT IF EXISTS viewers_handle_key;
CREATE UNIQUE INDEX IF NOT EXISTS ux_viewers_handle_case_insensitive
    ON viewers (lower(handle)) WHERE handle IS NOT NULL;

ALTER TABLE viewer_login_codes
    ADD COLUMN IF NOT EXISTS age_bracket varchar(12) NULL
        CHECK (age_bracket IS NULL OR age_bracket IN ('13_17', '18_plus')),
    ADD COLUMN IF NOT EXISTS streamer_id uuid NULL REFERENCES streamers(id);

CREATE TABLE IF NOT EXISTS viewer_refresh_tokens (
    id uuid PRIMARY KEY,
    viewer_id uuid NOT NULL REFERENCES viewers(id) ON DELETE CASCADE,
    session_family_id uuid NOT NULL,
    token_hash bytea NOT NULL UNIQUE,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,
    replaced_by_token_id uuid NULL
);
CREATE INDEX IF NOT EXISTS ix_viewer_refresh_tokens_viewer_live
    ON viewer_refresh_tokens(viewer_id, expires_at_utc DESC) WHERE revoked_at_utc IS NULL;

CREATE TABLE IF NOT EXISTS viewer_email_outbox (
    id uuid PRIMARY KEY,
    login_code_id uuid NOT NULL REFERENCES viewer_login_codes(id) ON DELETE CASCADE,
    protected_payload bytea NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    available_at_utc timestamptz NOT NULL DEFAULT now(),
    delivered_at_utc timestamptz NULL,
    cancelled_at_utc timestamptz NULL
);
CREATE INDEX IF NOT EXISTS ix_viewer_email_outbox_pending
    ON viewer_email_outbox(available_at_utc) WHERE delivered_at_utc IS NULL AND cancelled_at_utc IS NULL;

CREATE TABLE IF NOT EXISTS viewer_login_rate_limits (
    bucket_hash bytea PRIMARY KEY,
    bucket_type varchar(8) NOT NULL CHECK (bucket_type IN ('email', 'ip')),
    window_started_at_utc timestamptz NOT NULL,
    request_count integer NOT NULL CHECK (request_count >= 0)
);

DROP INDEX IF EXISTS ux_viewer_groups_global_name;
ALTER TABLE viewer_groups DROP CONSTRAINT IF EXISTS viewer_groups_viewer_id_streamer_id_name_key;
CREATE UNIQUE INDEX IF NOT EXISTS ux_viewer_groups_name_case_insensitive
    ON viewer_groups (viewer_id, COALESCE(streamer_id, '00000000-0000-0000-0000-000000000000'::uuid), lower(name));

CREATE TABLE IF NOT EXISTS reserved_viewer_handles (
    handle varchar(32) PRIMARY KEY,
    reason varchar(80) NOT NULL
);
INSERT INTO reserved_viewer_handles (handle, reason) VALUES
    ('admin', 'system'), ('administrator', 'system'), ('support', 'system'),
    ('system', 'system'), ('cardhub', 'brand'), ('easylottery', 'brand'),
    ('moderator', 'system'), ('official', 'system')
ON CONFLICT (handle) DO NOTHING;
