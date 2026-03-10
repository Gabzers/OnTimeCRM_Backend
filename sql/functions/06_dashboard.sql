-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: DASHBOARD
-- =============================================================================
-- fn_get_hot_deals       — active Hot clients for dashboard pipeline widget
-- fn_get_dashboard_kpis  — KPI counts and totals for the current month
-- fn_get_monthly_stats   — proposals vs sales per month (last N months)
-- fn_get_loss_reasons    — loss reason breakdown for charts
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_get_hot_deals
-- Returns active clients with temperature=Hot (0) not in a final stage.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_hot_deals(
    p_user_id UUID,
    p_limit   INT DEFAULT 10)
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
    created_at          TIMESTAMPTZ)
LANGUAGE sql AS $fn$
    SELECT
        c.id, c.full_name, c.phone, c.email,
        c.lead_source::INT, c.temperature::INT,
        c.current_stage_id,
        cs.name  AS stage_name,
        cs.color AS stage_color,
        c.last_interaction_at, c.created_at
    FROM clients c
    JOIN client_stages cs ON cs.id = c.current_stage_id
    WHERE c.user_id    = p_user_id
      AND c.is_active  = TRUE
      AND c.temperature = 0    -- Hot
      AND cs.is_final   = FALSE
    ORDER BY c.last_interaction_at DESC
    LIMIT p_limit;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_dashboard_kpis
-- All KPI values for the current calendar month.
-- Uses proposal_date and sold_at (business dates) — NOT created_at.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_dashboard_kpis(p_user_id UUID)
RETURNS TABLE (
    total_clients_active        BIGINT,
    total_proposals_this_month  BIGINT,
    total_sales_this_month      BIGINT,
    total_revenue_this_month    NUMERIC,
    overdue_notifications_count BIGINT)
LANGUAGE plpgsql AS $fn$
DECLARE
    v_month_start TIMESTAMPTZ := DATE_TRUNC('month', NOW() AT TIME ZONE 'UTC');
    v_month_end   TIMESTAMPTZ := v_month_start + INTERVAL '1 month';
BEGIN
    RETURN QUERY
    SELECT
        -- Active clients not in a final stage
        (SELECT COUNT(*) FROM clients c
         JOIN client_stages cs ON cs.id = c.current_stage_id
         WHERE c.user_id = p_user_id AND c.is_active = TRUE AND cs.is_final = FALSE),

        -- Proposals created (proposal_date) this month
        (SELECT COUNT(*) FROM proposals
         WHERE user_id = p_user_id
           AND proposal_date >= v_month_start AND proposal_date < v_month_end),

        -- Sales closed (sold_at) this month
        (SELECT COUNT(*) FROM sales
         WHERE user_id = p_user_id
           AND sold_at >= v_month_start AND sold_at < v_month_end),

        -- Revenue (sold_at) this month
        (SELECT COALESCE(SUM(final_value), 0) FROM sales
         WHERE user_id = p_user_id
           AND sold_at >= v_month_start AND sold_at < v_month_end),

        -- Overdue pending notifications
        (SELECT COUNT(*) FROM notifications
         WHERE user_id = p_user_id AND status = 0 AND scheduled_for < NOW());
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_monthly_stats
-- Returns proposals and sales aggregated by month for the last p_months months.
-- Used for the monthly bar chart on the dashboard.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_monthly_stats(
    p_user_id UUID,
    p_months  INT DEFAULT 12)
RETURNS TABLE (year INT, month INT, proposals INT, sales INT, revenue NUMERIC)
LANGUAGE plpgsql AS $fn$
DECLARE
    v_start TIMESTAMPTZ := DATE_TRUNC('month',
        NOW() AT TIME ZONE 'UTC' - ((p_months - 1) * INTERVAL '1 month'));
BEGIN
    RETURN QUERY
    WITH month_series AS (
        SELECT
            EXTRACT(YEAR  FROM v_start + (n * INTERVAL '1 month'))::INT AS yr,
            EXTRACT(MONTH FROM v_start + (n * INTERVAL '1 month'))::INT AS mo
        FROM generate_series(0, p_months - 1) AS n
    ),
    sale_agg AS (
        SELECT
            EXTRACT(YEAR  FROM sold_at)::INT AS yr,
            EXTRACT(MONTH FROM sold_at)::INT AS mo,
            COUNT(*)::INT                    AS cnt,
            SUM(final_value)                 AS rev
        FROM sales
        WHERE user_id = p_user_id AND sold_at >= v_start
        GROUP BY yr, mo
    ),
    proposal_agg AS (
        SELECT
            EXTRACT(YEAR  FROM proposal_date)::INT AS yr,
            EXTRACT(MONTH FROM proposal_date)::INT AS mo,
            COUNT(*)::INT                          AS cnt
        FROM proposals
        WHERE user_id = p_user_id AND proposal_date >= v_start
        GROUP BY yr, mo
    )
    SELECT
        ms.yr, ms.mo,
        COALESCE(pa.cnt, 0),
        COALESCE(sa.cnt, 0),
        COALESCE(sa.rev, 0::NUMERIC)
    FROM month_series ms
    LEFT JOIN sale_agg     sa ON sa.yr = ms.yr AND sa.mo = ms.mo
    LEFT JOIN proposal_agg pa ON pa.yr = ms.yr AND pa.mo = ms.mo
    ORDER BY ms.yr, ms.mo;
END;
$fn$;

-- ---------------------------------------------------------------------------
-- fn_get_loss_reasons
-- Returns loss reason distribution for the Loss Reasons chart.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_get_loss_reasons(p_user_id UUID)
RETURNS TABLE (reason INT, count INT)
LANGUAGE sql AS $fn$
    SELECT loss_reason::INT AS reason, COUNT(*)::INT AS count
    FROM   proposals
    WHERE  user_id = p_user_id AND status = 2 /* Lost */ AND loss_reason IS NOT NULL
    GROUP BY loss_reason
    ORDER BY count DESC;
$fn$;
