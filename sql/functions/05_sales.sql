-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: SALES
-- =============================================================================
-- fn_get_sales_paged — paginated list with optional year/month filter
-- fn_get_sale_by_id  — full sale detail with client and model info
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_sales_paged
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_sales_paged(
    p_user_id   UUID,
    p_year      INT DEFAULT NULL,
    p_month     INT DEFAULT NULL,
    p_page      INT DEFAULT 1,
    p_page_size INT DEFAULT 20)
RETURNS TABLE (
    id              UUID,
    client_id       UUID,
    client_name     TEXT,
    model_name      TEXT,
    free_text_model TEXT,
    final_value     NUMERIC,
    payment_type    INT,
    sold_at         TIMESTAMPTZ,
    total_count     BIGINT)
LANGUAGE plpgsql AS $fn$
DECLARE
    v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
    v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
BEGIN
    RETURN QUERY
    WITH base AS (
        SELECT
            s.id, s.client_id, c.full_name AS client_name,
            CONCAT(vb.name, ' ', vm.name) AS model_name,
            s.free_text_model,
            s.final_value, s.payment_type::INT, s.sold_at
        FROM sales s
        JOIN clients c ON c.id = s.client_id
        LEFT JOIN vehicle_models vm ON vm.id = s.model_id
        LEFT JOIN vehicle_brands vb ON vb.id = vm.brand_id
        WHERE s.user_id = p_user_id
          AND (p_year  IS NULL OR EXTRACT(YEAR  FROM s.sold_at) = p_year)
          AND (p_month IS NULL OR EXTRACT(MONTH FROM s.sold_at) = p_month)
    ),
    counted AS (SELECT COUNT(*) AS total FROM base)
    SELECT
        b.id, b.client_id, b.client_name, b.model_name, b.free_text_model,
        b.final_value, b.payment_type, b.sold_at,
        c.total AS total_count
    FROM base b, counted c
    ORDER BY b.sold_at DESC
    LIMIT v_size OFFSET v_offset;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_sale_by_id
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_sale_by_id(p_id UUID, p_user_id UUID)
RETURNS TABLE (
    id              UUID,
    proposal_id     UUID,
    client_id       UUID,
    client_name     TEXT,
    client_phone    TEXT,
    model_id        UUID,
    model_name      TEXT,
    free_text_model TEXT,
    final_value     NUMERIC,
    payment_type    INT,
    sold_at         TIMESTAMPTZ,
    plate           TEXT,
    chassis         TEXT,
    obs             TEXT,
    created_at      TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        s.id, s.proposal_id, s.client_id,
        c.full_name AS client_name, c.phone AS client_phone,
        s.model_id,
        CONCAT(vb.name, ' ', vm.name) AS model_name,
        s.free_text_model,
        s.final_value, s.payment_type::INT, s.sold_at,
        s.plate, s.chassis, s.obs,
        s.created_at
    FROM sales s
    JOIN clients c ON c.id = s.client_id
    LEFT JOIN vehicle_models vm ON vm.id = s.model_id
    LEFT JOIN vehicle_brands vb ON vb.id = vm.brand_id
    WHERE s.id = p_id AND s.user_id = p_user_id;
$fn$;
