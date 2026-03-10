-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: VEHICLES
-- =============================================================================
-- fn_get_vehicle_brands        — all vehicle brands ordered by name
-- fn_get_vehicle_models_paged  — paged models with brand/search filter
-- fn_get_vehicle_model_by_id   — single model with brand name
-- fn_create_vehicle_brand      — [ManagerOnly] add new brand
-- fn_delete_vehicle_brand      — [ManagerOnly] remove brand
-- fn_create_vehicle_model      — [ManagerOnly] add new model
-- fn_update_vehicle_model      — [ManagerOnly] edit model
-- fn_delete_vehicle_model      — [ManagerOnly] remove model
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_vehicle_brands
-- Returns all vehicle brands ordered alphabetically.
-- Shared across all companies (global catalogue).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_vehicle_brands()
RETURNS TABLE (
    id         UUID,
    name       TEXT,
    created_at TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT id, name, created_at
    FROM vehicle_brands
    ORDER BY name ASC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_vehicle_models_paged
-- Paged list filtered by optional brand_id and/or search term.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_vehicle_models_paged(
    p_brand_id   UUID    DEFAULT NULL,
    p_search     TEXT    DEFAULT NULL,
    p_page_index INT     DEFAULT 0,
    p_page_size  INT     DEFAULT 20)
RETURNS TABLE (
    id         UUID,
    name       TEXT,
    brand_id   UUID,
    brand_name TEXT,
    total_count BIGINT)
LANGUAGE sql AS $fn$
    WITH filtered AS (
        SELECT
            m.id, m.name,
            b.id   AS brand_id,
            b.name AS brand_name,
            COUNT(*) OVER () AS total_count
        FROM vehicle_models m
        INNER JOIN vehicle_brands b ON b.id = m.brand_id
        WHERE (p_brand_id IS NULL OR m.brand_id = p_brand_id)
          AND (p_search   IS NULL OR m.name ILIKE '%' || p_search || '%'
                                  OR b.name ILIKE '%' || p_search || '%')
    )
    SELECT id, name, brand_id, brand_name, total_count
    FROM filtered
    ORDER BY brand_name ASC, name ASC
    OFFSET (p_page_index * p_page_size)
    LIMIT  p_page_size;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_vehicle_model_by_id
-- Returns a single model with its brand name.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_vehicle_model_by_id(p_id UUID)
RETURNS TABLE (
    id         UUID,
    name       TEXT,
    brand_id   UUID,
    brand_name TEXT,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        m.id, m.name,
        b.id   AS brand_id,
        b.name AS brand_name,
        m.created_at, m.updated_at
    FROM vehicle_models m
    INNER JOIN vehicle_brands b ON b.id = m.brand_id
    WHERE m.id = p_id;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_vehicle_brand
-- Returns the new brand_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_vehicle_brand(p_name TEXT)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_id UUID := gen_random_uuid();
BEGIN
    INSERT INTO vehicle_brands (id, name, created_at)
    VALUES (v_id, p_name, NOW());
    RETURN v_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_delete_vehicle_brand
-- Deletes a brand by id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_delete_vehicle_brand(p_id UUID)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    DELETE FROM vehicle_brands WHERE id = p_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_vehicle_model
-- Returns the new model_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_vehicle_model(
    p_brand_id UUID,
    p_name     TEXT)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_id  UUID := gen_random_uuid();
    v_now TIMESTAMPTZ := NOW();
BEGIN
    INSERT INTO vehicle_models (id, brand_id, name, created_at, updated_at)
    VALUES (v_id, p_brand_id, p_name, v_now, v_now);
    RETURN v_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_vehicle_model
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_vehicle_model(
    p_id       UUID,
    p_brand_id UUID,
    p_name     TEXT)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE vehicle_models SET
        brand_id   = p_brand_id,
        name       = p_name,
        updated_at = NOW()
    WHERE id = p_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_delete_vehicle_model
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_delete_vehicle_model(p_id UUID)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    DELETE FROM vehicle_models WHERE id = p_id;
END;
$fn$;
