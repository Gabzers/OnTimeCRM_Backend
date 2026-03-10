-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: STAGES & TEMPLATES
-- =============================================================================
-- fn_get_stages_by_user    — all stages for a user with templates JSON
-- fn_create_stage          — create new stage (auto-ordered at end)
-- fn_update_stage          — update name, color, is_active
-- fn_delete_stage          — delete if no active clients (returns FALSE if blocked)
-- fn_reorder_stages        — bulk reorder by parallel arrays
-- fn_create_stage_template — add notification template to a stage
-- fn_update_stage_template — update template fields
-- fn_delete_stage_template — delete template (verifies stage ownership)
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_stages_by_user
-- Returns all stages with their templates serialised as a JSON string column.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_stages_by_user(p_user_id UUID)
RETURNS TABLE (
    id             UUID,
    name           TEXT,
    color          TEXT,
    stage_order    INT,
    is_final       BOOLEAN,
    is_won         BOOLEAN,
    is_lost        BOOLEAN,
    is_active      BOOLEAN,
    templates_json TEXT)
LANGUAGE sql AS $fn$
    SELECT
        s.id, s.name, s.color,
        s."order" AS stage_order,
        s.is_final, s.is_won, s.is_lost, s.is_active,
        COALESCE((
            SELECT json_agg(json_build_object(
                'id',         t.id,
                'title',      t.title,
                'days_after', t.days_after,
                'is_enabled', t.is_enabled)
                ORDER BY t.id)
            FILTER (WHERE t.id IS NOT NULL)
            FROM stage_notification_templates t
            WHERE t.stage_id = s.id
        ), '[]')::TEXT AS templates_json
    FROM client_stages s
    WHERE s.user_id = p_user_id
    ORDER BY s."order" ASC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_stage
-- Auto-assigns order = (max current order + 1).
-- Returns the new stage_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_stage(
    p_user_id UUID,
    p_name    TEXT,
    p_color   TEXT DEFAULT NULL)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_id    UUID := gen_random_uuid();
    v_order INT;
    v_now   TIMESTAMPTZ := NOW();
BEGIN
    SELECT COALESCE(MAX("order"), -1) + 1 INTO v_order
    FROM client_stages WHERE user_id = p_user_id;

    INSERT INTO client_stages (
        id, user_id, name, color, "order",
        is_final, is_won, is_lost, is_active,
        created_at, updated_at)
    VALUES (
        v_id, p_user_id, p_name, p_color, v_order,
        FALSE, FALSE, FALSE, TRUE,
        v_now, v_now);

    RETURN v_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_stage
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_stage(
    p_id        UUID,
    p_user_id   UUID,
    p_name      TEXT,
    p_color     TEXT    DEFAULT NULL,
    p_is_active BOOLEAN DEFAULT TRUE)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE client_stages SET
        name       = p_name,
        color      = p_color,
        is_active  = p_is_active,
        updated_at = NOW()
    WHERE id = p_id AND user_id = p_user_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_delete_stage
-- Returns FALSE if the stage has active clients (cannot delete).
-- Returns TRUE if successfully deleted.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_delete_stage(p_id UUID, p_user_id UUID)
RETURNS BOOLEAN
LANGUAGE plpgsql AS $fn$
BEGIN
    IF EXISTS (
        SELECT 1 FROM clients
        WHERE current_stage_id = p_id AND is_active = TRUE
    ) THEN
        RETURN FALSE;
    END IF;

    DELETE FROM client_stages WHERE id = p_id AND user_id = p_user_id;
    RETURN TRUE;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_reorder_stages
-- Atomically updates "order" for multiple stages.
-- p_ids and p_orders must be parallel arrays of the same length.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_reorder_stages(
    p_user_id UUID,
    p_ids     UUID[],
    p_orders  INT[])
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    FOR i IN 1..array_length(p_ids, 1) LOOP
        UPDATE client_stages SET "order" = p_orders[i], updated_at = NOW()
        WHERE id = p_ids[i] AND user_id = p_user_id;
    END LOOP;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_stage_template
-- Returns the new template_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_stage_template(
    p_stage_id   UUID,
    p_user_id    UUID,
    p_title      TEXT,
    p_days_after INT)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_id  UUID := gen_random_uuid();
    v_now TIMESTAMPTZ := NOW();
BEGIN
    -- Verify the stage belongs to the user
    IF NOT EXISTS (
        SELECT 1 FROM client_stages WHERE id = p_stage_id AND user_id = p_user_id
    ) THEN
        RAISE EXCEPTION 'STAGE_NOT_FOUND';
    END IF;

    INSERT INTO stage_notification_templates (
        id, stage_id, user_id, title, days_after, is_enabled,
        created_at, updated_at)
    VALUES (
        v_id, p_stage_id, p_user_id, p_title, p_days_after, TRUE,
        v_now, v_now);

    RETURN v_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_stage_template
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_stage_template(
    p_id         UUID,
    p_stage_id   UUID,
    p_user_id    UUID,
    p_title      TEXT,
    p_days_after INT,
    p_is_enabled BOOLEAN)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE stage_notification_templates t SET
        title      = p_title,
        days_after = p_days_after,
        is_enabled = p_is_enabled,
        updated_at = NOW()
    FROM client_stages s
    WHERE t.id       = p_id
      AND t.stage_id = p_stage_id
      AND s.id       = t.stage_id
      AND s.user_id  = p_user_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_delete_stage_template
-- Verifies stage ownership via JOIN before deleting.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_delete_stage_template(
    p_id       UUID,
    p_stage_id UUID,
    p_user_id  UUID)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    DELETE FROM stage_notification_templates t
    USING client_stages s
    WHERE t.id       = p_id
      AND t.stage_id = p_stage_id
      AND s.id       = t.stage_id
      AND s.user_id  = p_user_id;
END;
$fn$;
