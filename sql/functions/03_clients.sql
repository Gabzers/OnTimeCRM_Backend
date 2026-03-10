-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: CLIENTS
-- =============================================================================
-- fn_get_clients_paged       — paginated list with optional filters
-- fn_get_client_by_id        — single client detail with stage info
-- fn_get_client_history      — stage change history (ordered DESC)
-- fn_get_client_sales_history — all sales for a client
-- fn_create_client           — ATOMIC: client + first proposal + initial history
-- fn_update_client_stage     — ATOMIC: stage change + temperature + history + notifications
-- fn_soft_delete_client      — soft-deletes (sets is_active = FALSE)
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_clients_paged
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_clients_paged(
    p_user_id     UUID,
    p_stage_id    UUID  DEFAULT NULL,
    p_temperature INT   DEFAULT NULL,
    p_lead_source INT   DEFAULT NULL,
    p_search      TEXT  DEFAULT NULL,
    p_page        INT   DEFAULT 1,
    p_page_size   INT   DEFAULT 20)
RETURNS TABLE (
    id                  UUID,
    full_name           TEXT,
    phone               TEXT,
    email               TEXT,
    lead_source         INT,
    temperature         INT,
    current_stage_id    UUID,
    stage_name          TEXT,
    stage_color         TEXT,
    last_interaction_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ,
    total_count         BIGINT)
LANGUAGE plpgsql AS $fn$
DECLARE
    v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
    v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
BEGIN
    RETURN QUERY
    WITH base AS (
        SELECT
            c.id, c.full_name, c.phone, c.email,
            c.lead_source::INT, c.temperature::INT,
            c.current_stage_id,
            cs.name  AS stage_name,
            cs.color AS stage_color,
            c.last_interaction_at, c.created_at
        FROM clients c
        JOIN client_stages cs ON cs.id = c.current_stage_id
        WHERE c.user_id  = p_user_id
          AND c.is_active = TRUE
          AND (p_stage_id    IS NULL OR c.current_stage_id = p_stage_id)
          AND (p_temperature IS NULL OR c.temperature       = p_temperature)
          AND (p_lead_source IS NULL OR c.lead_source       = p_lead_source)
          AND (p_search      IS NULL
               OR c.full_name ILIKE '%' || p_search || '%'
               OR c.phone     ILIKE '%' || p_search || '%'
               OR c.email     ILIKE '%' || p_search || '%')
    ),
    counted AS (SELECT COUNT(*) AS total FROM base)
    SELECT
        b.id, b.full_name, b.phone, b.email,
        b.lead_source, b.temperature,
        b.current_stage_id, b.stage_name, b.stage_color,
        b.last_interaction_at, b.created_at,
        c.total AS total_count
    FROM base b, counted c
    ORDER BY COALESCE(b.last_interaction_at, b.created_at) DESC
    LIMIT v_size OFFSET v_offset;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_client_by_id
-- Returns NULL (empty resultset) if not found or not owned by user.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_client_by_id(p_id UUID, p_user_id UUID)
RETURNS TABLE (
    id                     UUID,
    full_name              TEXT,
    email                  TEXT,
    phone                  TEXT,
    tax_id                 TEXT,
    lead_source            INT,
    current_stage_id       UUID,
    current_stage_name     TEXT,
    current_stage_color    TEXT,
    current_stage_is_final BOOLEAN,
    current_stage_is_won   BOOLEAN,
    current_stage_is_lost  BOOLEAN,
    temperature            INT,
    last_interaction_at    TIMESTAMPTZ,
    created_at             TIMESTAMPTZ,
    updated_at             TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        c.id, c.full_name, c.email, c.phone, c.tax_id,
        c.lead_source::INT,
        cs.id, cs.name, cs.color,
        cs.is_final, cs.is_won, cs.is_lost,
        c.temperature::INT,
        c.last_interaction_at,
        c.created_at, c.updated_at
    FROM clients c
    JOIN client_stages cs ON cs.id = c.current_stage_id
    WHERE c.id = p_id AND c.user_id = p_user_id AND c.is_active = TRUE;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_client_history
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_client_history(p_client_id UUID)
RETURNS TABLE (
    id                UUID,
    from_stage_id     UUID,
    from_stage_name   TEXT,
    to_stage_id       UUID,
    to_stage_name     TEXT,
    to_stage_color    TEXT,
    obs               TEXT,
    proposal_snapshot TEXT,
    created_at        TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        h.id,
        h.from_stage_id,
        fs.name  AS from_stage_name,
        h.to_stage_id,
        ts.name  AS to_stage_name,
        ts.color AS to_stage_color,
        h.obs,
        h.proposal_snapshot,
        h.created_at
    FROM client_stage_histories h
    LEFT JOIN client_stages fs ON fs.id = h.from_stage_id
    JOIN      client_stages ts ON ts.id = h.to_stage_id
    WHERE h.client_id = p_client_id
    ORDER BY h.created_at DESC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_client_sales_history
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_client_sales_history(p_client_id UUID)
RETURNS TABLE (
    id              UUID,
    model_name      TEXT,
    free_text_model TEXT,
    final_value     NUMERIC,
    payment_type    INT,
    sold_at         TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        s.id,
        vm.name AS model_name,
        s.free_text_model,
        s.final_value,
        s.payment_type::INT,
        s.sold_at
    FROM sales s
    LEFT JOIN vehicle_models vm ON vm.id = s.model_id
    WHERE s.client_id = p_client_id
    ORDER BY s.sold_at DESC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_client
-- ATOMIC: creates client + first proposal (with vehicles) + initial stage history.
-- Vehicles JSON format: [{"model_id":"<uuid>","free_text_model":"text","is_preferred":true}]
-- Returns the new client_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_client(
    p_user_id            UUID,
    p_full_name          TEXT,
    p_email              TEXT        DEFAULT NULL,
    p_phone              TEXT        DEFAULT NULL,
    p_tax_id             TEXT        DEFAULT NULL,
    p_lead_source        INT         DEFAULT 0,
    p_business_type      INT         DEFAULT 0,
    p_payment_type       INT         DEFAULT 0,
    p_proposal_value     NUMERIC     DEFAULT NULL,
    p_proposal_date      TIMESTAMPTZ DEFAULT NULL,
    p_has_trade_in       BOOLEAN     DEFAULT FALSE,
    p_trade_in_type      INT         DEFAULT NULL,
    p_trade_in_plate     TEXT        DEFAULT NULL,
    p_trade_in_brand     TEXT        DEFAULT NULL,
    p_trade_in_model_txt TEXT        DEFAULT NULL,
    p_trade_in_year      INT         DEFAULT NULL,
    p_trade_in_km        INT         DEFAULT NULL,
    p_trade_in_est_value NUMERIC     DEFAULT NULL,
    p_vehicles           JSON        DEFAULT NULL)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_client_id   UUID := gen_random_uuid();
    v_proposal_id UUID := gen_random_uuid();
    v_stage_id    UUID;
    v_now         TIMESTAMPTZ := NOW();
    v_vehicle     JSON;
BEGIN
    -- Pick the first stage (lowest order) for this user
    SELECT id INTO v_stage_id
    FROM client_stages
    WHERE user_id = p_user_id AND is_active = TRUE
    ORDER BY "order" ASC LIMIT 1;

    IF v_stage_id IS NULL THEN
        RAISE EXCEPTION 'STAGE_NOT_FOUND';
    END IF;

    INSERT INTO clients (
        id, user_id, full_name, email, phone, tax_id, lead_source,
        current_stage_id, temperature, last_interaction_at,
        is_active, created_at, updated_at)
    VALUES (
        v_client_id, p_user_id, p_full_name, p_email, p_phone, p_tax_id, p_lead_source,
        v_stage_id, 1 /* Warm */, v_now, TRUE, v_now, v_now);

    INSERT INTO proposals (
        id, user_id, client_id, status, business_type, payment_type,
        proposal_value, proposal_date,
        has_trade_in, trade_in_type, trade_in_plate, trade_in_brand,
        trade_in_model, trade_in_year, trade_in_km, trade_in_estimated_value,
        created_at, updated_at)
    VALUES (
        v_proposal_id, p_user_id, v_client_id, 0 /* Active */,
        p_business_type, p_payment_type, p_proposal_value,
        COALESCE(p_proposal_date, v_now),
        p_has_trade_in, p_trade_in_type, p_trade_in_plate, p_trade_in_brand,
        p_trade_in_model_txt, p_trade_in_year, p_trade_in_km, p_trade_in_est_value,
        v_now, v_now);

    IF p_vehicles IS NOT NULL THEN
        FOR v_vehicle IN SELECT value FROM json_array_elements(p_vehicles) LOOP
            INSERT INTO proposal_vehicles (
                id, proposal_id, model_id, free_text_model, is_preferred,
                created_at, updated_at)
            VALUES (
                gen_random_uuid(), v_proposal_id,
                CASE WHEN v_vehicle->>'model_id' IS NOT NULL
                     THEN (v_vehicle->>'model_id')::UUID ELSE NULL END,
                v_vehicle->>'free_text_model',
                COALESCE((v_vehicle->>'is_preferred')::BOOLEAN, FALSE),
                v_now, v_now);
        END LOOP;
    END IF;

    -- Initial history entry (from_stage_id = NULL = creation)
    INSERT INTO client_stage_histories (
        id, client_id, user_id, from_stage_id, to_stage_id, obs,
        created_at, updated_at)
    VALUES (gen_random_uuid(), v_client_id, p_user_id, NULL, v_stage_id,
            'Cliente criado', v_now, v_now);

    RETURN v_client_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_client_stage
-- ATOMIC: update client stage + temperature + last_interaction_at
--         + insert history with proposal snapshot
--         + create notifications from stage templates
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_client_stage(
    p_client_id UUID,
    p_user_id   UUID,
    p_stage_id  UUID,
    p_obs       TEXT DEFAULT NULL)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_old_stage_id UUID;
    v_is_final     BOOLEAN;
    v_proposal_id  UUID;
    v_snapshot     TEXT;
    v_now          TIMESTAMPTZ := NOW();
BEGIN
    SELECT current_stage_id INTO v_old_stage_id
    FROM clients
    WHERE id = p_client_id AND user_id = p_user_id AND is_active = TRUE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'CLIENT_NOT_FOUND';
    END IF;

    SELECT is_final INTO v_is_final
    FROM client_stages WHERE id = p_stage_id AND user_id = p_user_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'STAGE_NOT_FOUND';
    END IF;

    -- Build proposal snapshot from the active proposal (status=0)
    SELECT p.id INTO v_proposal_id
    FROM proposals p
    WHERE p.client_id = p_client_id AND p.status = 0
    LIMIT 1;

    IF v_proposal_id IS NOT NULL THEN
        SELECT json_build_object(
            'pid', p.id,
            'pd',  p.proposal_date,
            'bt',  p.business_type,
            'pt',  p.payment_type,
            'val', p.proposal_value,
            'disc', p.discount,
            'tradeIn', p.has_trade_in,
            'ti', CASE WHEN p.has_trade_in THEN
                json_build_object(
                    'plate', p.trade_in_plate,
                    'brand', p.trade_in_brand,
                    'model', p.trade_in_model,
                    'year',  p.trade_in_year,
                    'km',    p.trade_in_km,
                    'est',   p.trade_in_estimated_value)
                ELSE NULL END,
            'vehicles', COALESCE((
                SELECT json_agg(json_build_object(
                    'mid',  pv.model_id,
                    'name', vm.name,
                    'pref', pv.is_preferred))
                FROM proposal_vehicles pv
                LEFT JOIN vehicle_models vm ON vm.id = pv.model_id
                WHERE pv.proposal_id = p.id), '[]'::json)
        )::TEXT INTO v_snapshot
        FROM proposals p WHERE p.id = v_proposal_id;
    END IF;

    -- Update client — final stages skip temperature recalculation
    IF v_is_final THEN
        UPDATE clients SET
            current_stage_id    = p_stage_id,
            last_interaction_at = v_now,
            updated_at          = v_now
        WHERE id = p_client_id;
    ELSE
        UPDATE clients SET
            current_stage_id    = p_stage_id,
            temperature         = 0, -- Hot (interaction just happened)
            last_interaction_at = v_now,
            updated_at          = v_now
        WHERE id = p_client_id;
    END IF;

    INSERT INTO client_stage_histories (
        id, client_id, user_id, from_stage_id, to_stage_id,
        obs, proposal_snapshot, created_at, updated_at)
    VALUES (
        gen_random_uuid(), p_client_id, p_user_id,
        v_old_stage_id, p_stage_id, p_obs, v_snapshot, v_now, v_now);

    -- Auto-create notifications from stage templates
    INSERT INTO notifications (
        id, user_id, client_id, proposal_id, trigger,
        status, title, scheduled_for, created_at, updated_at)
    SELECT
        gen_random_uuid(), p_user_id, p_client_id, v_proposal_id,
        1 /* StageChanged */, 0 /* Pending */,
        t.title,
        v_now + (t.days_after * INTERVAL '1 day'),
        v_now, v_now
    FROM stage_notification_templates t
    WHERE t.stage_id = p_stage_id AND t.is_enabled = TRUE;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_soft_delete_client
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_soft_delete_client(p_id UUID, p_user_id UUID)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE clients SET is_active = FALSE, updated_at = NOW()
    WHERE id = p_id AND user_id = p_user_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'CLIENT_NOT_FOUND';
    END IF;
END;
$fn$;
