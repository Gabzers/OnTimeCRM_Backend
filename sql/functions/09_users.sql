-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: USERS
-- =============================================================================
-- fn_get_users_by_brand  — all users belonging to a brand (manager-scoped)
-- fn_get_user_by_id      — single user with company + brand navigations
-- fn_update_user         — update profile fields (full_name, phone, email)
-- fn_update_user_active  — toggle is_active flag
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_users_by_brand
-- Returns all users whose brand_id matches the given value.
-- Used by ManagerOnly endpoints to list team members.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_users_by_brand(p_brand_id UUID)
RETURNS TABLE (
    id         UUID,
    email      TEXT,
    full_name  TEXT,
    phone      TEXT,
    role       SMALLINT,
    is_active  BOOLEAN,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        u.id, u.email, u.full_name, u.phone,
        u.role, u.is_active,
        u.created_at, u.updated_at
    FROM users u
    WHERE u.brand_id = p_brand_id
    ORDER BY u.full_name ASC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_user_by_id
-- Returns extended user info (with company and brand names/colors).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_user_by_id(p_id UUID)
RETURNS TABLE (
    id              UUID,
    email           TEXT,
    full_name       TEXT,
    phone           TEXT,
    role            SMALLINT,
    is_active       BOOLEAN,
    account_status  SMALLINT,
    brand_id        UUID,
    brand_name      TEXT,
    brand_color     TEXT,
    company_id      UUID,
    company_name    TEXT,
    created_at      TIMESTAMPTZ,
    updated_at      TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        u.id, u.email, u.full_name, u.phone,
        u.role, u.is_active, u.account_status,
        b.id   AS brand_id,
        b.name AS brand_name,
        b.color AS brand_color,
        c.id   AS company_id,
        c.name AS company_name,
        u.created_at, u.updated_at
    FROM users u
    INNER JOIN brands b ON b.id = u.brand_id
    INNER JOIN companies c ON c.id = u.company_id
    WHERE u.id = p_id;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_user
-- Partial update of user profile. NULL arguments preserve existing values.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_user(
    p_id        UUID,
    p_full_name TEXT    DEFAULT NULL,
    p_phone     TEXT    DEFAULT NULL,
    p_email     TEXT    DEFAULT NULL)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE users SET
        full_name  = COALESCE(p_full_name, full_name),
        phone      = COALESCE(p_phone,     phone),
        email      = COALESCE(p_email,     email),
        updated_at = NOW()
    WHERE id = p_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_user_active
-- Toggles the is_active flag for a user.
-- Returns the new is_active value.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_user_active(
    p_id        UUID,
    p_brand_id  UUID,
    p_is_active BOOLEAN)
RETURNS BOOLEAN
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE users SET
        is_active  = p_is_active,
        updated_at = NOW()
    WHERE id = p_id AND brand_id = p_brand_id;

    RETURN p_is_active;
END;
$fn$;
