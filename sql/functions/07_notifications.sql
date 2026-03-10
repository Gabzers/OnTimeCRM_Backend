-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: NOTIFICATIONS
-- =============================================================================
-- fn_get_today_notifications     — pending notifications due today or overdue
-- fn_get_notifications_paged     — paginated list with optional status filter
-- fn_get_overdue_count           — count of overdue pending notifications
-- fn_create_notification         — manual notification creation
-- fn_update_notification_done    — mark notification as Done
-- fn_update_notification_snoozed — snooze to a future date
-- fn_update_notification_ignored — mark notification as Ignored
-- fn_get_notification_prefs      — get notification preferences for a user
-- fn_update_notification_prefs   — update notification preferences (partial)
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_today_notifications
-- Returns Pending notifications where scheduled_for <= NOW() (overdue + today).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_today_notifications(p_user_id UUID)
RETURNS TABLE (
    id            UUID,
    client_id     UUID,
    client_name   TEXT,
    proposal_id   UUID,
    sale_id       UUID,
    trigger       INT,
    status        INT,
    title         TEXT,
    body          TEXT,
    scheduled_for TIMESTAMPTZ,
    done_at       TIMESTAMPTZ,
    snoozed_until TIMESTAMPTZ,
    created_at    TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        n.id, n.client_id, c.full_name,
        n.proposal_id, n.sale_id,
        n.trigger::INT, n.status::INT,
        n.title, n.body,
        n.scheduled_for, n.done_at, n.snoozed_until, n.created_at
    FROM notifications n
    LEFT JOIN clients c ON c.id = n.client_id
    WHERE n.user_id = p_user_id
      AND n.status  = 0    -- Pending
      AND n.scheduled_for <= NOW()
    ORDER BY n.scheduled_for ASC;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_notifications_paged
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_notifications_paged(
    p_user_id   UUID,
    p_status    INT  DEFAULT NULL,
    p_page      INT  DEFAULT 1,
    p_page_size INT  DEFAULT 20)
RETURNS TABLE (
    id            UUID,
    client_id     UUID,
    client_name   TEXT,
    proposal_id   UUID,
    sale_id       UUID,
    trigger       INT,
    status        INT,
    title         TEXT,
    body          TEXT,
    scheduled_for TIMESTAMPTZ,
    done_at       TIMESTAMPTZ,
    snoozed_until TIMESTAMPTZ,
    created_at    TIMESTAMPTZ,
    total_count   BIGINT)
LANGUAGE plpgsql AS $fn$
DECLARE
    v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
    v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
BEGIN
    RETURN QUERY
    WITH base AS (
        SELECT
            n.id, n.client_id, c.full_name AS client_name,
            n.proposal_id, n.sale_id,
            n.trigger::INT, n.status::INT,
            n.title, n.body,
            n.scheduled_for, n.done_at, n.snoozed_until, n.created_at
        FROM notifications n
        LEFT JOIN clients c ON c.id = n.client_id
        WHERE n.user_id = p_user_id
          AND (p_status IS NULL OR n.status = p_status)
    ),
    counted AS (SELECT COUNT(*) AS total FROM base)
    SELECT b.*, c.total AS total_count
    FROM base b, counted c
    ORDER BY b.scheduled_for DESC
    LIMIT v_size OFFSET v_offset;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_overdue_count
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_overdue_count(p_user_id UUID)
RETURNS BIGINT
LANGUAGE sql AS $fn$
    SELECT COUNT(*)
    FROM notifications
    WHERE user_id = p_user_id
      AND status = 0    -- Pending
      AND scheduled_for < NOW();
$fn$;

-- ---------------------------------------------------------------------------
-- fn_create_notification
-- Manual notification creation (trigger=0 Manual).
-- Returns the new notification id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_create_notification(
    p_user_id       UUID,
    p_client_id     UUID        DEFAULT NULL,
    p_proposal_id   UUID        DEFAULT NULL,
    p_sale_id       UUID        DEFAULT NULL,
    p_title         TEXT,
    p_body          TEXT        DEFAULT NULL,
    p_scheduled_for TIMESTAMPTZ)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
DECLARE
    v_id  UUID := gen_random_uuid();
    v_now TIMESTAMPTZ := NOW();
BEGIN
    INSERT INTO notifications (
        id, user_id, client_id, proposal_id, sale_id,
        trigger, status, title, body, scheduled_for,
        created_at, updated_at)
    VALUES (
        v_id, p_user_id, p_client_id, p_proposal_id, p_sale_id,
        0 /* Manual */, 0 /* Pending */,
        p_title, p_body, p_scheduled_for,
        v_now, v_now);

    RETURN v_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_notification_done
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_notification_done(p_id UUID, p_user_id UUID)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE notifications
    SET status = 1 /* Done */, done_at = NOW(), updated_at = NOW()
    WHERE id = p_id AND user_id = p_user_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_notification_snoozed
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_notification_snoozed(
    p_id      UUID,
    p_user_id UUID,
    p_until   TIMESTAMPTZ)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE notifications
    SET status = 2 /* Snoozed */, snoozed_until = p_until, updated_at = NOW()
    WHERE id = p_id AND user_id = p_user_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_notification_ignored
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_notification_ignored(p_id UUID, p_user_id UUID)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE notifications
    SET status = 3 /* Ignored */, updated_at = NOW()
    WHERE id = p_id AND user_id = p_user_id;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_notification_prefs
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_notification_prefs(p_user_id UUID)
RETURNS TABLE (
    daily_digest_time                    TIME,
    digest_frequency_days                INT,
    sale_follow_up_days                  INT,
    digest_enabled                       BOOLEAN,
    stage_change_notifications_enabled   BOOLEAN,
    sale_notifications_enabled           BOOLEAN)
LANGUAGE sql AS $fn$
    SELECT
        daily_digest_time,
        digest_frequency_days,
        sale_follow_up_days,
        digest_enabled,
        stage_change_notifications_enabled,
        sale_notifications_enabled
    FROM notification_preferences
    WHERE user_id = p_user_id;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_update_notification_prefs
-- Partial update — NULL parameters leave existing values unchanged.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_update_notification_prefs(
    p_user_id                            UUID,
    p_daily_digest_time                  TIME    DEFAULT NULL,
    p_digest_frequency_days              INT     DEFAULT NULL,
    p_sale_follow_up_days                INT     DEFAULT NULL,
    p_digest_enabled                     BOOLEAN DEFAULT NULL,
    p_stage_change_notifications_enabled BOOLEAN DEFAULT NULL,
    p_sale_notifications_enabled         BOOLEAN DEFAULT NULL)
RETURNS VOID
LANGUAGE plpgsql AS $fn$
BEGIN
    UPDATE notification_preferences SET
        daily_digest_time                  = COALESCE(p_daily_digest_time,                  daily_digest_time),
        digest_frequency_days              = COALESCE(p_digest_frequency_days,              digest_frequency_days),
        sale_follow_up_days                = COALESCE(p_sale_follow_up_days,                sale_follow_up_days),
        digest_enabled                     = COALESCE(p_digest_enabled,                     digest_enabled),
        stage_change_notifications_enabled = COALESCE(p_stage_change_notifications_enabled, stage_change_notifications_enabled),
        sale_notifications_enabled         = COALESCE(p_sale_notifications_enabled,         sale_notifications_enabled),
        updated_at                         = NOW()
    WHERE user_id = p_user_id;
END;
$fn$;
