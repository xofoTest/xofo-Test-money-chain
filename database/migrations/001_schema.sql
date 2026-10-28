-- ============================================================================
-- Money Chain SAS — PostgreSQL schema (canonical production schema)
-- The runnable local prototype uses SQLite via EF Core (no RLS support there);
-- this migration is the PostgreSQL target, with row-level security, constraints
-- and indexes as required. Currency is integer PAISE throughout (1 INR = 100).
-- ============================================================================
BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Auth context injected by the API from the verified JWT (set_config in a
-- connection hook): app.current_user_id and app.role.
CREATE OR REPLACE FUNCTION app.current_user_id() RETURNS uuid AS $$
  SELECT NULLIF(current_setting('app.current_user_id', true), '')::uuid;
$$ LANGUAGE sql STABLE;

CREATE OR REPLACE FUNCTION app.is_admin() RETURNS boolean AS $$
  SELECT COALESCE(current_setting('app.role', true), '') = 'admin';
$$ LANGUAGE sql STABLE;

-- ----------------------------------------------------------------------------
-- Tables
-- ----------------------------------------------------------------------------
CREATE TABLE profiles (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    member_code        text NOT NULL UNIQUE,
    full_name          text NOT NULL,
    phone              text,
    email              text NOT NULL UNIQUE,
    password_hash      text NOT NULL,
    preferred_language text NOT NULL DEFAULT 'en' CHECK (preferred_language IN ('en','ml')),
    role               text NOT NULL DEFAULT 'member' CHECK (role IN ('member','admin')),
    account_status     text NOT NULL DEFAULT 'pending' CHECK (account_status IN ('pending','active','suspended')),
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX profiles_status_idx ON profiles (account_status);

CREATE TABLE sponsorships (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sponsor_member_id   uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    sponsored_member_id uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    created_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (sponsored_member_id)
);
CREATE INDEX sponsorships_sponsor_idx ON sponsorships (sponsor_member_id);

CREATE TABLE placements (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id        uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    parent_member_id uuid REFERENCES profiles(id) ON DELETE RESTRICT,
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE (member_id)               -- one placement record per member (no duplicates)
);
CREATE INDEX placements_parent_idx ON placements (parent_member_id);

CREATE TABLE registration_records (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id      uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    amount_paise   integer NOT NULL CHECK (amount_paise >= 0),
    status         text NOT NULL DEFAULT 'simulated',
    is_simulated   boolean NOT NULL DEFAULT true,
    reference_code text NOT NULL UNIQUE,
    created_at     timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX registration_records_member_idx ON registration_records (member_id);

CREATE TABLE reward_ledger (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id        uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    reward_type      text NOT NULL,
    amount_paise     integer NOT NULL,
    status           text NOT NULL DEFAULT 'pending',
    is_simulated     boolean NOT NULL DEFAULT true,
    trigger_event_id text,
    policy_version   integer NOT NULL,
    description      text NOT NULL DEFAULT '',
    created_at       timestamptz NOT NULL DEFAULT now()
);
-- Immutability helper: duplicate rewards for the same qualifying event are blocked.
CREATE UNIQUE INDEX reward_ledger_trigger_unique ON reward_ledger (trigger_event_id)
    WHERE trigger_event_id IS NOT NULL;
CREATE INDEX reward_ledger_member_idx ON reward_ledger (member_id);

CREATE TABLE reward_policies (
    id                            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    policy_version                integer NOT NULL UNIQUE,
    direct_member_count_threshold integer NOT NULL DEFAULT 3,
    direct_member_reward_paise    integer NOT NULL DEFAULT 10000,  -- ₹100
    child_reward_paise            integer NOT NULL DEFAULT 1000,   -- ₹10
    child_reward_trigger          text NOT NULL DEFAULT 'child_completes_three_placements',
    simulation_enabled            boolean NOT NULL DEFAULT false,
    created_at                    timestamptz NOT NULL DEFAULT now(),
    updated_at                    timestamptz NOT NULL DEFAULT now()
);

-- Schema-only: withdrawal creation/processing stays disabled in the prototype.
CREATE TABLE withdrawal_requests (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    member_id    uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    amount_paise integer NOT NULL,
    status       text NOT NULL DEFAULT 'disabled',
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE admin_audit_logs (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id uuid NOT NULL REFERENCES profiles(id) ON DELETE RESTRICT,
    action        text NOT NULL,
    target_type   text NOT NULL,
    target_id     text NOT NULL,
    reason        text,
    metadata      jsonb NOT NULL DEFAULT '{}',
    created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX admin_audit_logs_created_idx ON admin_audit_logs (created_at DESC);

CREATE TABLE app_settings (
    key        text PRIMARY KEY,
    value      text NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- Placement invariants enforced at the database level (server also validates)
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION app.validate_placement() RETURNS trigger AS $$
DECLARE
    child_count integer;
    cur uuid;
BEGIN
    IF new.parent_member_id IS NOT NULL THEN
        IF new.parent_member_id = new.member_id THEN
            RAISE EXCEPTION 'A member cannot be their own parent';
        END IF;
        -- Walk up from the proposed parent; reaching the member means a cycle
        -- or placing a member beneath one of their own descendants.
        cur := new.parent_member_id;
        WHILE cur IS NOT NULL LOOP
            IF cur = new.member_id THEN
                RAISE EXCEPTION 'Placement would create a cycle or place under a descendant';
            END IF;
            SELECT parent_member_id INTO cur FROM placements WHERE member_id = cur;
        END LOOP;
        SELECT count(*) INTO child_count FROM placements WHERE parent_member_id = new.parent_member_id;
        IF child_count >= 3 THEN
            RAISE EXCEPTION 'Placement parent already has three children';
        END IF;
    END IF;
    RETURN new;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER placements_validate
    BEFORE INSERT OR UPDATE OF parent_member_id ON placements
    FOR EACH ROW EXECUTE FUNCTION app.validate_placement();

-- ----------------------------------------------------------------------------
-- Row-level security
-- ----------------------------------------------------------------------------
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE sponsorships ENABLE ROW LEVEL SECURITY;
ALTER TABLE placements ENABLE ROW LEVEL SECURITY;
ALTER TABLE registration_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE reward_ledger ENABLE ROW LEVEL SECURITY;
ALTER TABLE reward_policies ENABLE ROW LEVEL SECURITY;
ALTER TABLE withdrawal_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE admin_audit_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE app_settings ENABLE ROW LEVEL SECURITY;

-- profiles: a member can read/write their own row; admins read all.
CREATE POLICY profiles_select ON profiles FOR SELECT
    USING (app.is_admin() OR id = app.current_user_id());
CREATE POLICY profiles_insert ON profiles FOR INSERT
    WITH CHECK (app.is_admin() OR id = app.current_user_id());
CREATE POLICY profiles_update ON profiles FOR UPDATE
    USING (app.is_admin() OR id = app.current_user_id())
    WITH CHECK (app.is_admin() OR id = app.current_user_id());

-- sponsorships: own relationship (as sponsor or sponsored) or admin.
CREATE POLICY sponsorships_select ON sponsorships FOR SELECT
    USING (app.is_admin()
        OR sponsor_member_id = app.current_user_id()
        OR sponsored_member_id = app.current_user_id());

-- placements: own row, direct children, or admin.
CREATE POLICY placements_select ON placements FOR SELECT
    USING (app.is_admin()
        OR member_id = app.current_user_id()
        OR parent_member_id = app.current_user_id());

-- financial records: own only; immutable ledger (no update/delete policies).
CREATE POLICY registration_select ON registration_records FOR SELECT
    USING (app.is_admin() OR member_id = app.current_user_id());
CREATE POLICY reward_ledger_select ON reward_ledger FOR SELECT
    USING (app.is_admin() OR member_id = app.current_user_id());

-- reward policy values are readable (not private); writes are admin-only.
CREATE POLICY reward_policies_select ON reward_policies FOR SELECT USING (true);
CREATE POLICY reward_policies_write ON reward_policies FOR ALL
    USING (app.is_admin()) WITH CHECK (app.is_admin());

-- Withdrawals: no policies at all — creation and processing stay disabled.
-- admin_audit_logs and app_settings: admin-only.
CREATE POLICY audit_select ON admin_audit_logs FOR SELECT USING (app.is_admin());
CREATE POLICY audit_insert ON admin_audit_logs FOR INSERT WITH CHECK (app.is_admin());
CREATE POLICY settings_select ON app_settings FOR SELECT USING (app.is_admin());
CREATE POLICY settings_write ON app_settings FOR ALL
    USING (app.is_admin()) WITH CHECK (app.is_admin());

COMMIT;
