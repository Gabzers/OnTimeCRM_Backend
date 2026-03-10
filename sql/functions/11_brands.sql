-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: BRANDS
-- =============================================================================
-- fn_get_brands_by_company  — all brands scoped to a company with user_count
-- fn_get_brand_by_id         — single brand (ownership-checked by company_id)
-- fn_create_brand            — create a new brand under a company
-- fn_update_brand            — update brand name, color, and settings
-- fn_set_brand_active        — toggle is_active flag
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_brands_by_company
-- Returns all brands for a company, including active user count.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_brands_by_company(p_company_id UUID)
RETURNS TABLE (
    id                  UUID,
    name                TEXT,
    color               TEXT,
    is_active           BOOLEAN,
    sale_follow_up_days INT,
    user_count          BIGINT,
    created_at          TIMESTAMPTZ,
    updated_at          TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        b.id, b.name, b.color, b.is_active, b.sale_follow_up_days,
        COUNT(u.id) FILTER (WHERE u.is_active = TRUE) AS user_count,
        b.created_at, b.updated_at
    FROM brands b
    LEFT JOIN users u ON u.brand_id = b.id
    WHERE b.company_id = p_company_id
    GROUP BY b.id
    ORDER BY b.name ASC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_brand_by_id
-- Fetches a single brand, verifying it belongs to the given company.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_brand_by_id(p_id UUID, p_company_id UUID)
RETURNS TABLE (
    id                  UUID,
    name                TEXT,
    color               TEXT,
    is_active           BOOLEAN,
    sale_follow_up_days INT,
    company_id          UUID,
    company_name        TEXT,
    created_at          TIMESTAMPTZ,
    updated_at          TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        b.id, b.name, b.color, b.is_active, b.sale_follow_up_days,
        c.id   AS company_id,
        c.name AS company_name,
        b.created_at, b.updated_at
    FROM brands b
    INNER JOIN companies c ON c.id = b.company_id
    WHERE b.id = p_id
      AND b.company_id = p_company_id;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_brand
-- Creates a new brand under a company.
-- Returns the new brand_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_brand(
    p_company_id         UUID,
    p_name               TEXT,
    p_color              TEXT    DEFAULT '#1677FF',
    p_sale_follow_up_days INT    DEFAULT 30)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_id  UUID := gen_random_uuid();
    v_now TIMESTAMPTZ := NOW();
BEGIN
    INSERT INTO brands (
        id, company_id, name, color,
        is_active, sale_follow_up_days,
        created_at, updated_at)
    VALUES (
        v_id, p_company_id, p_name, p_color,
        TRUE, p_sale_follow_up_days,
        v_now, v_now);
    RETURN v_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_brand
-- Updates mutable brand fields.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_brand(
    p_id                  UUID,
    p_company_id          UUID,
    p_name                TEXT,
    p_color               TEXT   DEFAULT NULL,
    p_sale_follow_up_days INT    DEFAULT NULL)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE brands SET
        name                = p_name,
        color               = COALESCE(p_color,               color),
        sale_follow_up_days = COALESCE(p_sale_follow_up_days, sale_follow_up_days),
        updated_at          = NOW()
    WHERE id = p_id AND company_id = p_company_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_set_brand_active
-- Toggles is_active for a brand.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_set_brand_active(
    p_id         UUID,
    p_company_id UUID,
    p_is_active  BOOLEAN)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE brands SET
        is_active  = p_is_active,
        updated_at = NOW()
    WHERE id = p_id AND company_id = p_company_id;
END;
$fn$;
