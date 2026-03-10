-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: PROPOSALS
-- =============================================================================
-- fn_get_proposals_paged  — paginated list with optional filters
-- fn_get_proposal_by_id   — full detail including vehicles JSON
-- fn_create_proposal      — ATOMIC: proposal + vehicles
-- fn_update_proposal      — ATOMIC: update fields + replace vehicles
-- fn_mark_proposal_lost   — mark lost + move client to lost stage
-- fn_convert_proposal_to_sale — ATOMIC: sale + close proposal + won stage + notifications
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_proposals_paged
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_proposals_paged(
    p_user_id       UUID,
    p_status        INT         DEFAULT NULL,
    p_business_type INT         DEFAULT NULL,
    p_payment_type  INT         DEFAULT NULL,
    p_date_from     TIMESTAMPTZ DEFAULT NULL,
    p_date_to       TIMESTAMPTZ DEFAULT NULL,
    p_page          INT         DEFAULT 1,
    p_page_size     INT         DEFAULT 20)
RETURNS TABLE (
    id             UUID,
    client_id      UUID,
    client_name    TEXT,
    status         INT,
    business_type  INT,
    payment_type   INT,
    proposal_value NUMERIC,
    proposal_date  TIMESTAMPTZ,
    created_at     TIMESTAMPTZ,
    total_count    BIGINT)
LANGUAGE plpgsql AS $fn$
DECLARE
    v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
    v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
BEGIN
    RETURN QUERY
    WITH base AS (
        SELECT
            p.id, p.client_id, c.full_name AS client_name,
            p.status::INT, p.business_type::INT, p.payment_type::INT,
            p.proposal_value, p.proposal_date, p.created_at
        FROM proposals p
        JOIN clients c ON c.id = p.client_id
        WHERE p.user_id = p_user_id
          AND (p_status        IS NULL OR p.status        = p_status)
          AND (p_business_type IS NULL OR p.business_type = p_business_type)
          AND (p_payment_type  IS NULL OR p.payment_type  = p_payment_type)
          AND (p_date_from     IS NULL OR p.proposal_date >= p_date_from)
          AND (p_date_to       IS NULL OR p.proposal_date <= p_date_to)
    ),
    counted AS (SELECT COUNT(*) AS total FROM base)
    SELECT
        b.id, b.client_id, b.client_name,
        b.status, b.business_type, b.payment_type,
        b.proposal_value, b.proposal_date, b.created_at,
        c.total AS total_count
    FROM base b, counted c
    ORDER BY COALESCE(b.proposal_date, b.created_at) DESC
    LIMIT v_size OFFSET v_offset;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_proposal_by_id
-- Returns the proposal with vehicles serialised as a JSON string column.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_proposal_by_id(p_id UUID, p_user_id UUID)
RETURNS TABLE (
    id                       UUID,
    client_id                UUID,
    client_name              TEXT,
    status                   INT,
    business_type            INT,
    payment_type             INT,
    proposal_value           NUMERIC,
    discount                 NUMERIC,
    proposal_date            TIMESTAMPTZ,
    loss_reason              INT,
    loss_notes               TEXT,
    won_at                   TIMESTAMPTZ,
    lost_at                  TIMESTAMPTZ,
    has_trade_in             BOOLEAN,
    trade_in_type            INT,
    trade_in_plate           TEXT,
    trade_in_brand           TEXT,
    trade_in_model           TEXT,
    trade_in_year            INT,
    trade_in_km              INT,
    trade_in_estimated_value NUMERIC,
    vehicles_json            TEXT,
    created_at               TIMESTAMPTZ,
    updated_at               TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        p.id,
        p.client_id,
        c.full_name AS client_name,
        p.status::INT,
        p.business_type::INT,
        p.payment_type::INT,
        p.proposal_value,
        p.discount,
        p.proposal_date,
        p.loss_reason::INT,
        p.loss_notes,
        p.won_at,
        p.lost_at,
        p.has_trade_in,
        p.trade_in_type::INT,
        p.trade_in_plate,
        p.trade_in_brand,
        p.trade_in_model,
        p.trade_in_year,
        p.trade_in_km,
        p.trade_in_estimated_value,
        COALESCE((
            SELECT json_agg(json_build_object(
                'id',              pv.id,
                'model_id',        pv.model_id,
                'model_name',      vm.name,
                'brand_name',      vb.name,
                'free_text_model', pv.free_text_model,
                'is_preferred',    pv.is_preferred))
            FROM proposal_vehicles pv
            LEFT JOIN vehicle_models vm ON vm.id = pv.model_id
            LEFT JOIN vehicle_brands vb ON vb.id = vm.brand_id
            WHERE pv.proposal_id = p.id
        ), '[]')::TEXT AS vehicles_json,
        p.created_at,
        p.updated_at
    FROM proposals p
    JOIN clients c ON c.id = p.client_id
    WHERE p.id = p_id AND p.user_id = p_user_id;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_proposal
-- ATOMIC: creates proposal + vehicles. Returns new proposal_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_proposal(
    p_user_id            UUID,
    p_client_id          UUID,
    p_business_type      INT,
    p_payment_type       INT,
    p_proposal_value     NUMERIC     DEFAULT NULL,
    p_discount           NUMERIC     DEFAULT NULL,
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
    v_proposal_id UUID := gen_random_uuid();
    v_now         TIMESTAMPTZ := NOW();
    v_vehicle     JSON;
BEGIN
    INSERT INTO proposals (
        id, user_id, client_id, status, business_type, payment_type,
        proposal_value, discount, proposal_date,
        has_trade_in, trade_in_type, trade_in_plate, trade_in_brand,
        trade_in_model, trade_in_year, trade_in_km, trade_in_estimated_value,
        created_at, updated_at)
    VALUES (
        v_proposal_id, p_user_id, p_client_id, 0 /* Active */,
        p_business_type, p_payment_type, p_proposal_value, p_discount,
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

    RETURN v_proposal_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_proposal
-- ATOMIC: replaces all vehicles + updates proposal fields.
-- Only works on Active proposals (status = 0).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_proposal(
    p_id                 UUID,
    p_user_id            UUID,
    p_business_type      INT,
    p_payment_type       INT,
    p_proposal_value     NUMERIC     DEFAULT NULL,
    p_discount           NUMERIC     DEFAULT NULL,
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
RETURNS VOID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_now     TIMESTAMPTZ := NOW();
    v_vehicle JSON;
BEGIN
    UPDATE proposals SET
        business_type            = p_business_type,
        payment_type             = p_payment_type,
        proposal_value           = p_proposal_value,
        discount                 = p_discount,
        proposal_date            = COALESCE(p_proposal_date, proposal_date),
        has_trade_in             = p_has_trade_in,
        trade_in_type            = p_trade_in_type,
        trade_in_plate           = p_trade_in_plate,
        trade_in_brand           = p_trade_in_brand,
        trade_in_model           = p_trade_in_model_txt,
        trade_in_year            = p_trade_in_year,
        trade_in_km              = p_trade_in_km,
        trade_in_estimated_value = p_trade_in_est_value,
        updated_at               = v_now
    WHERE id = p_id AND user_id = p_user_id AND status = 0 /* Active */;

    -- Replace vehicles
    DELETE FROM proposal_vehicles WHERE proposal_id = p_id;

    IF p_vehicles IS NOT NULL THEN
        FOR v_vehicle IN SELECT value FROM json_array_elements(p_vehicles) LOOP
            INSERT INTO proposal_vehicles (
                id, proposal_id, model_id, free_text_model, is_preferred,
                created_at, updated_at)
            VALUES (
                gen_random_uuid(), p_id,
                CASE WHEN v_vehicle->>'model_id' IS NOT NULL
                     THEN (v_vehicle->>'model_id')::UUID ELSE NULL END,
                v_vehicle->>'free_text_model',
                COALESCE((v_vehicle->>'is_preferred')::BOOLEAN, FALSE),
                v_now, v_now);
        END LOOP;
    END IF;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_mark_proposal_lost
-- Marks proposal Lost + optionally moves client to the Lost stage.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_mark_proposal_lost(
    p_id          UUID,
    p_user_id     UUID,
    p_loss_reason INT,
    p_loss_notes  TEXT DEFAULT NULL)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_client_id  UUID;
    v_lost_stage UUID;
    v_now        TIMESTAMPTZ := NOW();
BEGIN
    UPDATE proposals SET
        status      = 2, -- Lost
        loss_reason = p_loss_reason,
        loss_notes  = p_loss_notes,
        lost_at     = v_now,
        updated_at  = v_now
    WHERE id = p_id AND user_id = p_user_id AND status = 0 /* Active */
    RETURNING client_id INTO v_client_id;

    -- If client not already in a lost stage, move them
    IF v_client_id IS NOT NULL THEN
        SELECT id INTO v_lost_stage
        FROM client_stages WHERE user_id = p_user_id AND is_lost = TRUE LIMIT 1;

        IF v_lost_stage IS NOT NULL THEN
            UPDATE clients SET
                current_stage_id    = v_lost_stage,
                last_interaction_at = v_now,
                updated_at          = v_now
            WHERE id = v_client_id
              AND id NOT IN (
                  SELECT c2.id FROM clients c2
                  JOIN client_stages cs ON cs.id = c2.current_stage_id
                  WHERE c2.id = v_client_id AND cs.is_lost = TRUE);
        END IF;
    END IF;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_convert_proposal_to_sale
-- ATOMIC: creates Sale + marks proposal Won + moves client to Won stage
--         + inserts history with snapshot + inserts post-sale notifications.
-- CRITICAL: p_sold_at is ALWAYS user-provided — NEVER uses NOW().
-- Returns the new sale_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_convert_proposal_to_sale(
    p_proposal_id     UUID,
    p_user_id         UUID,
    p_model_id        UUID        DEFAULT NULL,
    p_free_text_model TEXT        DEFAULT NULL,
    p_final_value     NUMERIC,
    p_payment_type    INT,
    p_sold_at         TIMESTAMPTZ,   -- MUST come from request, never auto-set
    p_plate           TEXT        DEFAULT NULL,
    p_chassis         TEXT        DEFAULT NULL,
    p_obs             TEXT        DEFAULT NULL)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_sale_id        UUID := gen_random_uuid();
    v_client_id      UUID;
    v_old_stage_id   UUID;
    v_won_stage_id   UUID;
    v_follow_up_days INT;
    v_snapshot       TEXT;
    v_now            TIMESTAMPTZ := NOW();  -- system timestamp for audit fields only
BEGIN
    SELECT p.client_id, c.current_stage_id
    INTO   v_client_id, v_old_stage_id
    FROM   proposals p
    JOIN   clients   c ON c.id = p.client_id
    WHERE  p.id = p_proposal_id AND p.user_id = p_user_id AND p.status = 0 /* Active */;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'PROPOSAL_NOT_ACTIVE';
    END IF;

    -- Snapshot before marking Won
    SELECT json_build_object(
        'pid',    p.id,
        'pd',     p.proposal_date,
        'bt',     p.business_type,
        'pt',     p.payment_type,
        'val',    p.proposal_value,
        'disc',   p.discount,
        'tradeIn', p.has_trade_in,
        'ti', CASE WHEN p.has_trade_in THEN json_build_object(
            'plate', p.trade_in_plate, 'brand', p.trade_in_brand,
            'model', p.trade_in_model, 'year', p.trade_in_year,
            'km',    p.trade_in_km,   'est',  p.trade_in_estimated_value)
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
    FROM proposals p WHERE p.id = p_proposal_id;

    -- Create sale — sold_at comes from p_sold_at (NEVER NOW())
    INSERT INTO sales (
        id, proposal_id, client_id, user_id, model_id, free_text_model,
        final_value, payment_type, sold_at, plate, chassis, obs,
        created_at, updated_at)
    VALUES (
        v_sale_id, p_proposal_id, v_client_id, p_user_id,
        p_model_id, p_free_text_model, p_final_value, p_payment_type,
        p_sold_at,  -- business date from user
        p_plate, p_chassis, p_obs, v_now, v_now);

    -- Close proposal as Won
    UPDATE proposals SET status = 1 /* Won */, won_at = v_now, updated_at = v_now
    WHERE id = p_proposal_id;

    -- Move client to Won stage + history + post-sale notification
    SELECT id INTO v_won_stage_id
    FROM client_stages WHERE user_id = p_user_id AND is_won = TRUE LIMIT 1;

    IF v_won_stage_id IS NOT NULL THEN
        INSERT INTO client_stage_histories (
            id, client_id, user_id, from_stage_id, to_stage_id,
            obs, proposal_snapshot, created_at, updated_at)
        VALUES (
            gen_random_uuid(), v_client_id, p_user_id,
            v_old_stage_id, v_won_stage_id,
            'Venda concluída', v_snapshot, v_now, v_now);

        UPDATE clients SET
            current_stage_id    = v_won_stage_id,
            last_interaction_at = v_now,
            updated_at          = v_now
        WHERE id = v_client_id;

        SELECT COALESCE(sale_follow_up_days, 30) INTO v_follow_up_days
        FROM notification_preferences WHERE user_id = p_user_id;

        v_follow_up_days := COALESCE(v_follow_up_days, 30);

        -- Post-sale follow-up notifications from Won stage templates
        INSERT INTO notifications (
            id, user_id, client_id, proposal_id, sale_id,
            trigger, status, title, scheduled_for, created_at, updated_at)
        SELECT
            gen_random_uuid(), p_user_id, v_client_id, p_proposal_id, v_sale_id,
            2 /* SaleClosed */, 0 /* Pending */,
            t.title,
            v_now + (v_follow_up_days * INTERVAL '1 day'),
            v_now, v_now
        FROM stage_notification_templates t
        WHERE t.stage_id = v_won_stage_id AND t.is_enabled = TRUE;
    END IF;

    RETURN v_sale_id;
END;
$fn$;
