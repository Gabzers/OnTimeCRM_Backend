namespace OnTimeCRM.Infrastructure.Persistence.Sql;

/// <summary>
/// ALL PostgreSQL function definitions (CREATE OR REPLACE).
/// Applied at startup via DatabaseInitializer.InitializeAsync.
///
/// Enum reference (for raw SQL):
///   DealTemperature    : Hot=0   Warm=1   Cold=2
///   ProposalStatus     : Active=0  Won=1  Lost=2  Cancelled=3
///   NotificationStatus : Pending=0  Done=1  Snoozed=2  Ignored=3
///   NotificationTrigger: Manual=0  StageChanged=1  SaleClosed=2  ProposalCreated=3
///   UserRole           : Salesperson=0  Manager=1
///   UserAccountStatus  : PendingActivation=0  Active=1
/// </summary>
public static class DatabaseFunctions
{
    // ════════════════════════════════════════════════════════════════════════
    // CLIENTS
    // ════════════════════════════════════════════════════════════════════════

    public const string GetClientsPaged = """
        CREATE OR REPLACE FUNCTION fn_get_clients_paged(
            p_user_id     UUID,
            p_brand_id    UUID  DEFAULT NULL,
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
        LANGUAGE sql AS $fn$
            SELECT
                c.id,
                c.full_name::TEXT,
                c.phone::TEXT,
                c.email::TEXT,
                c.lead_source::INT,
                c.temperature::INT,
                c.current_stage_id,
                cs.name::TEXT  AS stage_name,
                cs.color::TEXT AS stage_color,
                c.last_interaction_at,
                c.created_at,
                COUNT(*) OVER()::BIGINT AS total_count
            FROM clients c
            JOIN client_stages cs ON cs.id = c.current_stage_id
            WHERE c.is_active = TRUE
              AND (
                  (p_brand_id IS NOT NULL AND c.user_id IN (
                      SELECT u.id FROM users u
                      WHERE u.brand_id = p_brand_id AND u.is_active = TRUE
                  ))
                  OR (p_brand_id IS NULL AND c.user_id = p_user_id)
              )
              AND (p_stage_id    IS NULL OR c.current_stage_id = p_stage_id)
              AND (p_temperature IS NULL OR c.temperature       = p_temperature)
              AND (p_lead_source IS NULL OR c.lead_source       = p_lead_source)
              AND (p_search      IS NULL
                   OR c.full_name ILIKE '%' || p_search || '%'
                   OR c.phone     ILIKE '%' || p_search || '%'
                   OR c.email     ILIKE '%' || p_search || '%')
            ORDER BY COALESCE(c.last_interaction_at, c.created_at) DESC
            LIMIT  LEAST(GREATEST(p_page_size, 1), 50)
            OFFSET (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
        $fn$;
        """;

    public const string GetClientById = """
        CREATE OR REPLACE FUNCTION fn_get_client_by_id(
            p_id      UUID,
            p_user_id UUID)
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
        """;

    public const string GetClientHistory = """
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
            JOIN  client_stages ts ON ts.id = h.to_stage_id
            WHERE h.client_id = p_client_id
            ORDER BY h.created_at DESC;
        $fn$;
        """;

    public const string GetClientSalesHistory = """
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
        """;

    /// <summary>
    /// ATOMIC: creates client + first proposal (with vehicles) + initial stage history.
    /// Vehicles JSON format: [{"model_id":"guid","free_text_model":"text","is_preferred":true}]
    /// </summary>
    public const string CreateClient = """
        CREATE OR REPLACE FUNCTION fn_create_client(
            p_user_id            UUID,
            p_full_name          TEXT,
            p_email              TEXT       DEFAULT NULL,
            p_phone              TEXT       DEFAULT NULL,
            p_tax_id             TEXT       DEFAULT NULL,
            p_lead_source        INT        DEFAULT 0,
            p_business_type      INT        DEFAULT 0,
            p_payment_type       INT        DEFAULT 0,
            p_proposal_value     NUMERIC    DEFAULT NULL,
            p_proposal_date      TIMESTAMPTZ DEFAULT NULL,
            p_has_trade_in       BOOLEAN    DEFAULT FALSE,
            p_trade_in_type      INT        DEFAULT NULL,
            p_trade_in_plate     TEXT       DEFAULT NULL,
            p_trade_in_brand     TEXT       DEFAULT NULL,
            p_trade_in_model_txt TEXT       DEFAULT NULL,
            p_trade_in_year      INT        DEFAULT NULL,
            p_trade_in_km        INT        DEFAULT NULL,
            p_trade_in_est_value NUMERIC    DEFAULT NULL,
            p_vehicles           JSON       DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_client_id   UUID := gen_random_uuid();
            v_proposal_id UUID := gen_random_uuid();
            v_stage_id    UUID;
            v_now         TIMESTAMPTZ := NOW();
            v_vehicle     JSON;
        BEGIN
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
                v_stage_id, 1, v_now, TRUE, v_now, v_now);

            INSERT INTO proposals (
                id, user_id, client_id, status, business_type, payment_type,
                proposal_value, proposal_date,
                has_trade_in, trade_in_type, trade_in_plate, trade_in_brand,
                trade_in_model, trade_in_year, trade_in_km, trade_in_estimated_value,
                created_at, updated_at)
            VALUES (
                v_proposal_id, p_user_id, v_client_id, 0,
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

            INSERT INTO client_stage_histories (
                id, client_id, user_id, from_stage_id, to_stage_id, obs,
                created_at, updated_at)
            VALUES (gen_random_uuid(), v_client_id, p_user_id, NULL, v_stage_id,
                    'Cliente criado', v_now, v_now);

            RETURN v_client_id;
        END;
        $fn$;
        """;

    /// <summary>
    /// ATOMIC stage change: updates client stage + temperature + last_interaction_at
    /// + inserts history with proposal snapshot + inserts notifications from templates.
    /// </summary>
    public const string UpdateClientStage = """
        CREATE OR REPLACE FUNCTION fn_update_client_stage(
            p_client_id UUID,
            p_user_id   UUID,
            p_stage_id  UUID,
            p_obs       TEXT DEFAULT NULL)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
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

            -- Build proposal snapshot from the active proposal
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

            -- Update client
            IF v_is_final THEN
                UPDATE clients SET
                    current_stage_id    = p_stage_id,
                    last_interaction_at = v_now,
                    updated_at          = v_now
                WHERE id = p_client_id;
            ELSE
                UPDATE clients SET
                    current_stage_id    = p_stage_id,
                    temperature         = 0,
                    last_interaction_at = v_now,
                    updated_at          = v_now
                WHERE id = p_client_id;
            END IF;

            -- Insert history
            INSERT INTO client_stage_histories (
                id, client_id, user_id, from_stage_id, to_stage_id,
                obs, proposal_snapshot, created_at, updated_at)
            VALUES (
                gen_random_uuid(), p_client_id, p_user_id,
                v_old_stage_id, p_stage_id, p_obs, v_snapshot, v_now, v_now);

            -- Insert notifications from templates
            INSERT INTO notifications (
                id, user_id, client_id, proposal_id, trigger,
                status, title, scheduled_for, created_at, updated_at)
            SELECT
                gen_random_uuid(), p_user_id, p_client_id, v_proposal_id,
                1, 0,
                t.title,
                v_now + (t.days_after * INTERVAL '1 day'),
                v_now, v_now
            FROM stage_notification_templates t
            WHERE t.stage_id = p_stage_id AND t.is_enabled = TRUE;
        END;
        $fn$;
        """;

    public const string SoftDeleteClient = """
        CREATE OR REPLACE FUNCTION fn_soft_delete_client(p_id UUID, p_user_id UUID)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE clients SET is_active = FALSE, updated_at = NOW()
            WHERE id = p_id AND user_id = p_user_id;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'CLIENT_NOT_FOUND';
            END IF;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // PROPOSALS
    // ════════════════════════════════════════════════════════════════════════

    public const string GetProposalsPaged = """
        CREATE OR REPLACE FUNCTION fn_get_proposals_paged(
            p_user_id       UUID,
            p_status        INT         DEFAULT NULL,
            p_business_type INT         DEFAULT NULL,
            p_payment_type  INT         DEFAULT NULL,
            p_date_from     TIMESTAMPTZ DEFAULT NULL,
            p_date_to       TIMESTAMPTZ DEFAULT NULL,
            p_stage_id      UUID        DEFAULT NULL,
            p_search        TEXT        DEFAULT NULL,
            p_client_id     UUID        DEFAULT NULL,
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
            vehicle_name   TEXT,
            total_count    BIGINT)
        LANGUAGE plpgsql AS $fn$
        DECLARE
            v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
            v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
        BEGIN
            RETURN QUERY
            SELECT
                p.id,
                p.client_id,
                c.full_name::TEXT                AS client_name,
                p.status::INT                    AS status,
                p.business_type::INT             AS business_type,
                p.payment_type::INT              AS payment_type,
                p.proposal_value::NUMERIC        AS proposal_value,
                p.proposal_date::TIMESTAMPTZ     AS proposal_date,
                p.created_at::TIMESTAMPTZ        AS created_at,
                COALESCE(
                    (SELECT CONCAT(vb.name, ' ', vm.name)
                     FROM proposal_vehicles pv
                     LEFT JOIN vehicle_models vm ON vm.id = pv.model_id
                     LEFT JOIN vehicle_brands vb ON vb.id = vm.brand_id
                     WHERE pv.proposal_id = p.id
                     ORDER BY pv.is_preferred DESC LIMIT 1),
                    (SELECT pv2.free_text_model
                     FROM proposal_vehicles pv2
                     WHERE pv2.proposal_id = p.id
                       AND pv2.free_text_model IS NOT NULL
                     ORDER BY pv2.is_preferred DESC LIMIT 1)
                )::TEXT                          AS vehicle_name,
                COUNT(*) OVER()::BIGINT          AS total_count
            FROM proposals p
            JOIN clients c ON c.id = p.client_id
            WHERE p.user_id = p_user_id
              AND (p_status        IS NULL OR p.status        = p_status)
              AND (p_business_type IS NULL OR p.business_type = p_business_type)
              AND (p_payment_type  IS NULL OR p.payment_type  = p_payment_type)
              AND (p_date_from     IS NULL OR p.proposal_date >= p_date_from)
              AND (p_date_to       IS NULL OR p.proposal_date <= p_date_to)
              AND (p_stage_id      IS NULL OR c.current_stage_id = p_stage_id)
              AND (p_client_id     IS NULL OR p.client_id = p_client_id)
              AND (p_search        IS NULL
                   OR c.full_name ILIKE '%' || p_search || '%')
            ORDER BY COALESCE(p.proposal_date, p.created_at) DESC
            LIMIT v_size OFFSET v_offset;
        END;
        $fn$;
        """;

    /// <summary>Returns proposal detail with vehicles serialised as a JSON string column.</summary>
    public const string GetProposalById = """
        CREATE OR REPLACE FUNCTION fn_get_proposal_by_id(p_id UUID, p_user_id UUID)
        RETURNS TABLE (
            id                     UUID,
            client_id              UUID,
            client_name            TEXT,
            status                 INT,
            business_type          INT,
            payment_type           INT,
            proposal_value         NUMERIC,
            discount               NUMERIC,
            proposal_date          TIMESTAMPTZ,
            loss_reason            INT,
            loss_notes             TEXT,
            won_at                 TIMESTAMPTZ,
            lost_at                TIMESTAMPTZ,
            has_trade_in           BOOLEAN,
            trade_in_type          INT,
            trade_in_plate         TEXT,
            trade_in_brand         TEXT,
            trade_in_model         TEXT,
            trade_in_year          INT,
            trade_in_km            INT,
            trade_in_estimated_value NUMERIC,
            vehicles_json          TEXT,
            created_at             TIMESTAMPTZ,
            updated_at             TIMESTAMPTZ)
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
        """;

    /// <summary>ATOMIC: creates proposal + vehicles. Returns new proposal id.</summary>
    public const string CreateProposal = """
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
        RETURNS UUID LANGUAGE plpgsql AS $fn$
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
                v_proposal_id, p_user_id, p_client_id, 0,
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
        """;

    /// <summary>ATOMIC: replaces vehicles + updates proposal fields.</summary>
    public const string UpdateProposal = """
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
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_now     TIMESTAMPTZ := NOW();
            v_vehicle JSON;
        BEGIN
            UPDATE proposals SET
                business_type          = p_business_type,
                payment_type           = p_payment_type,
                proposal_value         = p_proposal_value,
                discount               = p_discount,
                proposal_date          = COALESCE(p_proposal_date, proposal_date),
                has_trade_in           = p_has_trade_in,
                trade_in_type          = p_trade_in_type,
                trade_in_plate         = p_trade_in_plate,
                trade_in_brand         = p_trade_in_brand,
                trade_in_model         = p_trade_in_model_txt,
                trade_in_year          = p_trade_in_year,
                trade_in_km            = p_trade_in_km,
                trade_in_estimated_value = p_trade_in_est_value,
                updated_at             = v_now
            WHERE id = p_id AND user_id = p_user_id AND status = 0;

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
        """;

    public const string MarkProposalLost = """
        CREATE OR REPLACE FUNCTION fn_mark_proposal_lost(
            p_id          UUID,
            p_user_id     UUID,
            p_loss_reason INT,
            p_loss_notes  TEXT DEFAULT NULL)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_client_id UUID;
            v_lost_stage UUID;
            v_now        TIMESTAMPTZ := NOW();
        BEGIN
            UPDATE proposals SET
                status      = 2,
                loss_reason = p_loss_reason,
                loss_notes  = p_loss_notes,
                lost_at     = v_now,
                updated_at  = v_now
            WHERE id = p_id AND user_id = p_user_id AND status = 0
            RETURNING client_id INTO v_client_id;

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
        """;

    /// <summary>
    /// ATOMIC: creates Sale + marks proposal Won + moves client to won stage
    /// + inserts history with snapshot + inserts post-sale notifications.
    /// SoldAt ALWAYS comes from p_sold_at — NEVER UtcNow.
    /// Returns the new sale id.
    /// </summary>
    public const string ConvertProposalToSale = """
        CREATE OR REPLACE FUNCTION fn_convert_proposal_to_sale(
            p_proposal_id     UUID,
            p_user_id         UUID,
            p_final_value     NUMERIC,
            p_payment_type    INT,
            p_sold_at         TIMESTAMPTZ,
            p_model_id        UUID        DEFAULT NULL,
            p_free_text_model TEXT        DEFAULT NULL,
            p_plate           TEXT        DEFAULT NULL,
            p_chassis         TEXT        DEFAULT NULL,
            p_obs             TEXT        DEFAULT NULL,
            p_commission      NUMERIC     DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_sale_id        UUID := gen_random_uuid();
            v_client_id      UUID;
            v_old_stage_id   UUID;
            v_won_stage_id   UUID;
            v_follow_up_days INT;
            v_snapshot       TEXT;
            v_now            TIMESTAMPTZ := NOW();
        BEGIN
            SELECT p.client_id, c.current_stage_id
            INTO   v_client_id, v_old_stage_id
            FROM   proposals p
            JOIN   clients   c ON c.id = p.client_id
            WHERE  p.id = p_proposal_id AND p.user_id = p_user_id AND p.status = 0;

            IF NOT FOUND THEN
                RAISE EXCEPTION 'PROPOSAL_NOT_ACTIVE';
            END IF;

            -- Build snapshot before marking Won
            SELECT json_build_object(
                'pid', p.id, 'pd', p.proposal_date,
                'bt', p.business_type, 'pt', p.payment_type,
                'val', p.proposal_value, 'disc', p.discount,
                'tradeIn', p.has_trade_in,
                'ti', CASE WHEN p.has_trade_in THEN json_build_object(
                    'plate', p.trade_in_plate, 'brand', p.trade_in_brand,
                    'model', p.trade_in_model, 'year', p.trade_in_year,
                    'km', p.trade_in_km, 'est', p.trade_in_estimated_value)
                    ELSE NULL END,
                'vehicles', COALESCE((
                    SELECT json_agg(json_build_object('mid', pv.model_id, 'name', vm.name, 'pref', pv.is_preferred))
                    FROM proposal_vehicles pv
                    LEFT JOIN vehicle_models vm ON vm.id = pv.model_id
                    WHERE pv.proposal_id = p.id), '[]'::json)
            )::TEXT INTO v_snapshot
            FROM proposals p WHERE p.id = p_proposal_id;

            -- Create sale
            INSERT INTO sales (
                id, proposal_id, client_id, user_id, model_id, free_text_model,
                final_value, payment_type, sold_at, plate, chassis, obs, commission,
                created_at, updated_at)
            VALUES (
                v_sale_id, p_proposal_id, v_client_id, p_user_id,
                p_model_id, p_free_text_model, p_final_value, p_payment_type,
                p_sold_at, p_plate, p_chassis, p_obs, p_commission, v_now, v_now);

            -- Mark proposal Won
            UPDATE proposals SET status = 1, won_at = v_now, updated_at = v_now
            WHERE id = p_proposal_id;

            -- Move client to Won stage
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

                INSERT INTO notifications (
                    id, user_id, client_id, proposal_id, sale_id,
                    trigger, status, title, scheduled_for, created_at, updated_at)
                SELECT
                    gen_random_uuid(), p_user_id, v_client_id, p_proposal_id, v_sale_id,
                    2, 0, t.title,
                    v_now + (v_follow_up_days * INTERVAL '1 day'),
                    v_now, v_now
                FROM stage_notification_templates t
                WHERE t.stage_id = v_won_stage_id AND t.is_enabled = TRUE;
            END IF;

            RETURN v_sale_id;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // SALES
    // ════════════════════════════════════════════════════════════════════════

    public const string GetSalesPaged = """
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
            plate           TEXT,
            commission      NUMERIC,
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
                    CONCAT(vb.name, ' ', vm.name) AS model_name, s.free_text_model,
                    s.final_value, s.payment_type::INT, s.sold_at,
                    s.plate, s.commission
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
                b.plate, b.commission,
                c.total AS total_count
            FROM base b, counted c
            ORDER BY b.sold_at DESC
            LIMIT v_size OFFSET v_offset;
        END;
        $fn$;
        """;

    public const string GetSaleById = """
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
            commission      NUMERIC,
            created_at      TIMESTAMPTZ)
        LANGUAGE sql AS $fn$
            SELECT
                s.id, s.proposal_id, s.client_id,
                c.full_name AS client_name, c.phone AS client_phone,
                s.model_id,
                CONCAT(vb.name, ' ', vm.name) AS model_name,
                s.free_text_model,
                s.final_value, s.payment_type::INT, s.sold_at,
                s.plate, s.chassis, s.obs, s.commission,
                s.created_at
            FROM sales s
            JOIN clients c ON c.id = s.client_id
            LEFT JOIN vehicle_models vm ON vm.id = s.model_id
            LEFT JOIN vehicle_brands vb ON vb.id = vm.brand_id
            WHERE s.id = p_id AND s.user_id = p_user_id;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // DASHBOARD  (existing — unchanged)
    // ════════════════════════════════════════════════════════════════════════

    public const string GetHotDeals = """
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
              AND c.temperature = 0
              AND cs.is_final   = FALSE
            ORDER BY c.last_interaction_at DESC
            LIMIT p_limit;
        $fn$;
        """;

    public const string GetDashboardKpis = """
        CREATE OR REPLACE FUNCTION fn_get_dashboard_kpis(p_user_id UUID)
        RETURNS TABLE (
            total_clients_active        BIGINT,
            total_proposals_this_month  BIGINT,
            total_sales_this_month      BIGINT,
            total_revenue_this_month    NUMERIC,
            total_commission_this_month NUMERIC,
            overdue_notifications_count BIGINT)
        LANGUAGE plpgsql AS $fn$
        DECLARE
            v_month_start TIMESTAMPTZ := DATE_TRUNC('month', NOW() AT TIME ZONE 'UTC');
            v_month_end   TIMESTAMPTZ := v_month_start + INTERVAL '1 month';
        BEGIN
            RETURN QUERY
            SELECT
                (SELECT COUNT(*) FROM clients c
                 JOIN client_stages cs ON cs.id = c.current_stage_id
                 WHERE c.user_id = p_user_id AND c.is_active AND NOT cs.is_final),
                (SELECT COUNT(*) FROM proposals
                 WHERE user_id = p_user_id
                   AND proposal_date >= v_month_start AND proposal_date < v_month_end),
                (SELECT COUNT(*) FROM sales
                 WHERE user_id = p_user_id
                   AND sold_at >= v_month_start AND sold_at < v_month_end),
                (SELECT COALESCE(SUM(final_value), 0) FROM sales
                 WHERE user_id = p_user_id
                   AND sold_at >= v_month_start AND sold_at < v_month_end),
                (SELECT COALESCE(SUM(commission), 0) FROM sales
                 WHERE user_id = p_user_id
                   AND sold_at >= v_month_start AND sold_at < v_month_end),
                (SELECT COUNT(*) FROM notifications
                 WHERE user_id = p_user_id AND status = 0 AND scheduled_for < NOW());
        END;
        $fn$;
        """;

    public const string GetMonthlyStats = """
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
                SELECT EXTRACT(YEAR FROM sold_at)::INT AS yr, EXTRACT(MONTH FROM sold_at)::INT AS mo,
                       COUNT(*)::INT AS cnt, SUM(final_value) AS rev
                FROM sales WHERE user_id = p_user_id AND sold_at >= v_start
                GROUP BY yr, mo
            ),
            proposal_agg AS (
                SELECT EXTRACT(YEAR FROM proposal_date)::INT AS yr, EXTRACT(MONTH FROM proposal_date)::INT AS mo,
                       COUNT(*)::INT AS cnt
                FROM proposals WHERE user_id = p_user_id AND proposal_date >= v_start
                GROUP BY yr, mo
            )
            SELECT ms.yr, ms.mo,
                   COALESCE(pa.cnt, 0), COALESCE(sa.cnt, 0), COALESCE(sa.rev, 0::NUMERIC)
            FROM month_series ms
            LEFT JOIN sale_agg     sa ON sa.yr = ms.yr AND sa.mo = ms.mo
            LEFT JOIN proposal_agg pa ON pa.yr = ms.yr AND pa.mo = ms.mo
            ORDER BY ms.yr, ms.mo;
        END;
        $fn$;
        """;

    public const string GetLossReasons = """
        CREATE OR REPLACE FUNCTION fn_get_loss_reasons(p_user_id UUID)
        RETURNS TABLE (reason INT, count INT)
        LANGUAGE sql AS $fn$
            SELECT loss_reason::INT AS reason, COUNT(*)::INT AS count
            FROM   proposals
            WHERE  user_id = p_user_id AND status = 2 AND loss_reason IS NOT NULL
            GROUP BY loss_reason ORDER BY count DESC;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // NOTIFICATIONS
    // ════════════════════════════════════════════════════════════════════════

    public const string GetTodayNotifications = """
        CREATE OR REPLACE FUNCTION fn_get_today_notifications(p_user_id UUID)
        RETURNS TABLE (
            id            UUID, client_id UUID, client_name TEXT,
            proposal_id   UUID, sale_id    UUID,
            trigger       INT,  status     INT,
            title         TEXT, body       TEXT,
            scheduled_for TIMESTAMPTZ, done_at TIMESTAMPTZ,
            snoozed_until TIMESTAMPTZ, created_at TIMESTAMPTZ)
        LANGUAGE sql AS $fn$
            SELECT n.id, n.client_id, c.full_name,
                   n.proposal_id, n.sale_id,
                   n.trigger::INT, n.status::INT,
                   n.title, n.body,
                   n.scheduled_for, n.done_at, n.snoozed_until, n.created_at
            FROM notifications n
            LEFT JOIN clients c ON c.id = n.client_id
            WHERE n.user_id = p_user_id AND n.status = 0 AND n.scheduled_for <= NOW()
            ORDER BY n.scheduled_for ASC;
        $fn$;
        """;

    public const string GetNotificationsPaged = """
        CREATE OR REPLACE FUNCTION fn_get_notifications_paged(
            p_user_id   UUID,
            p_status    INT  DEFAULT NULL,
            p_page      INT  DEFAULT 1,
            p_page_size INT  DEFAULT 20)
        RETURNS TABLE (
            id            UUID, client_id UUID, client_name TEXT,
            proposal_id   UUID, sale_id    UUID,
            trigger       INT,  status     INT,
            title         TEXT, body       TEXT,
            scheduled_for TIMESTAMPTZ, done_at TIMESTAMPTZ,
            snoozed_until TIMESTAMPTZ, created_at TIMESTAMPTZ,
            total_count   BIGINT)
        LANGUAGE plpgsql AS $fn$
        DECLARE
            v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
            v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
        BEGIN
            RETURN QUERY
            WITH base AS (
                SELECT n.id, n.client_id, c.full_name AS client_name,
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
        """;

    public const string GetOverdueCount = """
        CREATE OR REPLACE FUNCTION fn_get_overdue_count(p_user_id UUID)
        RETURNS BIGINT LANGUAGE sql AS $fn$
            SELECT COUNT(*) FROM notifications
            WHERE user_id = p_user_id AND status = 0 AND scheduled_for < NOW();
        $fn$;
        """;

    public const string CreateNotification = """
        CREATE OR REPLACE FUNCTION fn_create_notification(
            p_user_id       UUID,
            p_title         TEXT,
            p_scheduled_for TIMESTAMPTZ,
            p_client_id     UUID        DEFAULT NULL,
            p_proposal_id   UUID        DEFAULT NULL,
            p_sale_id       UUID        DEFAULT NULL,
            p_body          TEXT        DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_id  UUID := gen_random_uuid();
            v_now TIMESTAMPTZ := NOW();
        BEGIN
            INSERT INTO notifications (
                id, user_id, client_id, proposal_id, sale_id,
                trigger, status, title, body, scheduled_for, created_at, updated_at)
            VALUES (
                v_id, p_user_id, p_client_id, p_proposal_id, p_sale_id,
                0, 0, p_title, p_body, p_scheduled_for, v_now, v_now);
            RETURN v_id;
        END;
        $fn$;
        """;

    public const string UpdateNotificationDone = """
        CREATE OR REPLACE FUNCTION fn_update_notification_done(p_id UUID, p_user_id UUID)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE notifications SET status = 1, done_at = NOW(), updated_at = NOW()
            WHERE id = p_id AND user_id = p_user_id;
        END;
        $fn$;
        """;

    public const string UpdateNotificationSnoozed = """
        CREATE OR REPLACE FUNCTION fn_update_notification_snoozed(
            p_id    UUID,
            p_user_id UUID,
            p_until TIMESTAMPTZ)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE notifications
            SET status = 2, snoozed_until = p_until, updated_at = NOW()
            WHERE id = p_id AND user_id = p_user_id;
        END;
        $fn$;
        """;

    public const string UpdateNotificationIgnored = """
        CREATE OR REPLACE FUNCTION fn_update_notification_ignored(p_id UUID, p_user_id UUID)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE notifications SET status = 3, updated_at = NOW()
            WHERE id = p_id AND user_id = p_user_id;
        END;
        $fn$;
        """;

    public const string GetNotificationPrefs = """
        CREATE OR REPLACE FUNCTION fn_get_notification_prefs(p_user_id UUID)
        RETURNS TABLE (
            daily_digest_time                  TIME,
            digest_frequency_days              INT,
            sale_follow_up_days                INT,
            digest_enabled                     BOOLEAN,
            stage_change_notifications_enabled BOOLEAN,
            sale_notifications_enabled         BOOLEAN)
        LANGUAGE sql AS $fn$
            SELECT daily_digest_time, digest_frequency_days, sale_follow_up_days,
                   digest_enabled, stage_change_notifications_enabled, sale_notifications_enabled
            FROM notification_preferences WHERE user_id = p_user_id;
        $fn$;
        """;

    public const string UpdateNotificationPrefs = """
        CREATE OR REPLACE FUNCTION fn_update_notification_prefs(
            p_user_id                          UUID,
            p_daily_digest_time                TIME        DEFAULT NULL,
            p_digest_frequency_days            INT         DEFAULT NULL,
            p_sale_follow_up_days              INT         DEFAULT NULL,
            p_digest_enabled                   BOOLEAN     DEFAULT NULL,
            p_stage_change_notifications_enabled BOOLEAN   DEFAULT NULL,
            p_sale_notifications_enabled       BOOLEAN     DEFAULT NULL)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE notification_preferences SET
                daily_digest_time                  = COALESCE(p_daily_digest_time, daily_digest_time),
                digest_frequency_days              = COALESCE(p_digest_frequency_days, digest_frequency_days),
                sale_follow_up_days                = COALESCE(p_sale_follow_up_days, sale_follow_up_days),
                digest_enabled                     = COALESCE(p_digest_enabled, digest_enabled),
                stage_change_notifications_enabled = COALESCE(p_stage_change_notifications_enabled, stage_change_notifications_enabled),
                sale_notifications_enabled         = COALESCE(p_sale_notifications_enabled, sale_notifications_enabled),
                updated_at                         = NOW()
            WHERE user_id = p_user_id;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // STAGES
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Returns all stages for a user with their templates as a JSON string column.</summary>
    public const string GetStagesByUser = """
        CREATE OR REPLACE FUNCTION fn_get_stages_by_user(p_user_id UUID)
        RETURNS TABLE (
            id            UUID,
            name          TEXT,
            color         TEXT,
            stage_order   INT,
            is_final      BOOLEAN,
            is_won        BOOLEAN,
            is_lost       BOOLEAN,
            is_active     BOOLEAN,
            templates_json TEXT)
        LANGUAGE sql AS $fn$
            SELECT
                s.id, s.name, s.color, s."order" AS stage_order,
                s.is_final, s.is_won, s.is_lost, s.is_active,
                COALESCE((
                    SELECT json_agg(json_build_object(
                        'id',         t.id,
                        'title',      t.title,
                        'days_after', t.days_after,
                        'is_enabled', t.is_enabled)
                        ORDER BY t.id)
                    FILTER (WHERE t.id IS NOT NULL)
                    FROM stage_notification_templates t WHERE t.stage_id = s.id
                ), '[]')::TEXT AS templates_json
            FROM client_stages s
            WHERE s.user_id = p_user_id
            ORDER BY s."order" ASC;
        $fn$;
        """;

    public const string CreateStage = """
        CREATE OR REPLACE FUNCTION fn_create_stage(
            p_user_id  UUID,
            p_name     TEXT,
            p_color    TEXT DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_id    UUID := gen_random_uuid();
            v_order INT;
            v_now   TIMESTAMPTZ := NOW();
        BEGIN
            SELECT COALESCE(MAX("order"), -1) + 1 INTO v_order
            FROM client_stages WHERE user_id = p_user_id;

            INSERT INTO client_stages (
                id, user_id, name, color, "order",
                is_final, is_won, is_lost, is_active, created_at, updated_at)
            VALUES (v_id, p_user_id, p_name, p_color, v_order,
                    FALSE, FALSE, FALSE, TRUE, v_now, v_now);
            RETURN v_id;
        END;
        $fn$;
        """;

    public const string UpdateStage = """
        CREATE OR REPLACE FUNCTION fn_update_stage(
            p_id        UUID,
            p_user_id   UUID,
            p_name      TEXT,
            p_color     TEXT    DEFAULT NULL,
            p_is_active BOOLEAN DEFAULT TRUE)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE client_stages SET
                name      = p_name,
                color     = p_color,
                is_active = p_is_active,
                updated_at = NOW()
            WHERE id = p_id AND user_id = p_user_id;
        END;
        $fn$;
        """;

    /// <summary>Returns FALSE if stage has active clients (cannot delete), TRUE if deleted.</summary>
    public const string DeleteStage = """
        CREATE OR REPLACE FUNCTION fn_delete_stage(p_id UUID, p_user_id UUID)
        RETURNS BOOLEAN LANGUAGE plpgsql AS $fn$
        BEGIN
            IF EXISTS (SELECT 1 FROM clients WHERE current_stage_id = p_id AND is_active = TRUE) THEN
                RETURN FALSE;
            END IF;
            DELETE FROM client_stages WHERE id = p_id AND user_id = p_user_id;
            RETURN TRUE;
        END;
        $fn$;
        """;

    /// <summary>Updates "order" for multiple stages atomically. p_ids and p_orders must be same length.</summary>
    public const string ReorderStages = """
        CREATE OR REPLACE FUNCTION fn_reorder_stages(
            p_user_id UUID,
            p_ids     UUID[],
            p_orders  INT[])
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            FOR i IN 1..array_length(p_ids, 1) LOOP
                UPDATE client_stages SET "order" = p_orders[i], updated_at = NOW()
                WHERE id = p_ids[i] AND user_id = p_user_id;
            END LOOP;
        END;
        $fn$;
        """;

    public const string CreateStageTemplate = """
        CREATE OR REPLACE FUNCTION fn_create_stage_template(
            p_stage_id  UUID,
            p_user_id   UUID,
            p_title     TEXT,
            p_days_after INT)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_id  UUID := gen_random_uuid();
            v_now TIMESTAMPTZ := NOW();
        BEGIN
            -- Verify stage belongs to user
            IF NOT EXISTS (SELECT 1 FROM client_stages WHERE id = p_stage_id AND user_id = p_user_id) THEN
                RAISE EXCEPTION 'STAGE_NOT_FOUND';
            END IF;
            INSERT INTO stage_notification_templates (
                id, stage_id, title, days_after, is_enabled, created_at, updated_at)
            VALUES (v_id, p_stage_id, p_title, p_days_after, TRUE, v_now, v_now);
            RETURN v_id;
        END;
        $fn$;
        """;

    public const string UpdateStageTemplate = """
        CREATE OR REPLACE FUNCTION fn_update_stage_template(
            p_id         UUID,
            p_stage_id   UUID,
            p_user_id    UUID,
            p_title      TEXT,
            p_days_after INT,
            p_is_enabled BOOLEAN)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE stage_notification_templates t SET
                title      = p_title,
                days_after = p_days_after,
                is_enabled = p_is_enabled,
                updated_at = NOW()
            FROM client_stages s
            WHERE t.id = p_id AND t.stage_id = p_stage_id
              AND s.id = t.stage_id AND s.user_id = p_user_id;
        END;
        $fn$;
        """;

    public const string DeleteStageTemplate = """
        CREATE OR REPLACE FUNCTION fn_delete_stage_template(
            p_id       UUID,
            p_stage_id UUID,
            p_user_id  UUID)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            DELETE FROM stage_notification_templates t
            USING client_stages s
            WHERE t.id = p_id AND t.stage_id = p_stage_id
              AND s.id = t.stage_id AND s.user_id = p_user_id;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // USERS
    // ════════════════════════════════════════════════════════════════════════

    public const string GetUsersByBrand = """
        CREATE OR REPLACE FUNCTION fn_get_users_by_brand(p_brand_id UUID)
        RETURNS TABLE (
            id             UUID,
            full_name      TEXT,
            email          TEXT,
            phone          TEXT,
            role           INT,
            account_status INT,
            created_at     TIMESTAMPTZ)
        LANGUAGE sql AS $fn$
            SELECT id, full_name, email, phone,
                   role::INT, account_status::INT, created_at
            FROM users
            WHERE brand_id = p_brand_id
            ORDER BY full_name;
        $fn$;
        """;

    public const string GetUserById = """
        CREATE OR REPLACE FUNCTION fn_get_user_by_id(p_id UUID)
        RETURNS TABLE (
            id             UUID,
            company_id     UUID,
            brand_id       UUID,
            full_name      TEXT,
            email          TEXT,
            password_hash  TEXT,
            phone          TEXT,
            role           INT,
            account_status INT,
            is_active      BOOLEAN,
            company_name   TEXT,
            company_is_active BOOLEAN,
            brand_name     TEXT,
            brand_color    TEXT,
            brand_is_active BOOLEAN)
        LANGUAGE sql AS $fn$
            SELECT
                u.id, u.company_id, u.brand_id,
                u.full_name, u.email, u.password_hash, u.phone,
                u.role::INT, u.account_status::INT, u.is_active,
                comp.name AS company_name, comp.is_active AS company_is_active,
                b.name AS brand_name, b.primary_color AS brand_color, b.is_active AS brand_is_active
            FROM users u
            JOIN companies comp ON comp.id = u.company_id
            JOIN brands    b    ON b.id    = u.brand_id
            WHERE u.id = p_id;
        $fn$;
        """;

    public const string UpdateUser = """
        CREATE OR REPLACE FUNCTION fn_update_user(
            p_id        UUID,
            p_full_name TEXT,
            p_phone     TEXT DEFAULT NULL,
            p_email     TEXT DEFAULT NULL)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE users SET
                full_name  = p_full_name,
                phone      = p_phone,
                email      = COALESCE(p_email, email),
                updated_at = NOW()
            WHERE id = p_id;
        END;
        $fn$;
        """;

    public const string UpdateUserActive = """
        CREATE OR REPLACE FUNCTION fn_update_user_active(p_id UUID, p_is_active BOOLEAN)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE users SET is_active = p_is_active, updated_at = NOW()
            WHERE id = p_id;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // VEHICLES
    // ════════════════════════════════════════════════════════════════════════

    public const string GetVehicleBrands = """
        CREATE OR REPLACE FUNCTION fn_get_vehicle_brands()
        RETURNS TABLE (id UUID, name TEXT, logo_url TEXT)
        LANGUAGE sql AS $fn$
            SELECT id, name, logo_url FROM vehicle_brands ORDER BY name;
        $fn$;
        """;

    public const string GetVehicleModelsPaged = """
        CREATE OR REPLACE FUNCTION fn_get_vehicle_models_paged(
            p_brand_id  UUID DEFAULT NULL,
            p_search    TEXT DEFAULT NULL,
            p_page      INT  DEFAULT 1,
            p_page_size INT  DEFAULT 20)
        RETURNS TABLE (
            id          UUID,
            brand_id    UUID,
            brand_name  TEXT,
            name        TEXT,
            version     TEXT,
            year        INT,
            fuel_type   INT,
            total_count BIGINT)
        LANGUAGE plpgsql AS $fn$
        DECLARE
            v_offset INT := (GREATEST(p_page, 1) - 1) * LEAST(GREATEST(p_page_size, 1), 50);
            v_size   INT := LEAST(GREATEST(p_page_size, 1), 50);
        BEGIN
            RETURN QUERY
            WITH base AS (
                SELECT m.id, m.brand_id, b.name AS brand_name,
                       m.name, m.version, m.year, m.fuel_type::INT
                FROM vehicle_models m
                JOIN vehicle_brands b ON b.id = m.brand_id
                WHERE (p_brand_id IS NULL OR m.brand_id = p_brand_id)
                  AND (p_search   IS NULL
                       OR m.name    ILIKE '%' || p_search || '%'
                       OR b.name    ILIKE '%' || p_search || '%')
            ),
            counted AS (SELECT COUNT(*) AS total FROM base)
            SELECT b.id, b.brand_id, b.brand_name, b.name, b.version, b.year, b.fuel_type,
                   c.total AS total_count
            FROM base b, counted c
            ORDER BY b.brand_name, b.name
            LIMIT v_size OFFSET v_offset;
        END;
        $fn$;
        """;

    public const string GetVehicleModelById = """
        CREATE OR REPLACE FUNCTION fn_get_vehicle_model_by_id(p_id UUID)
        RETURNS TABLE (
            id          UUID,
            brand_id    UUID,
            brand_name  TEXT,
            name        TEXT,
            version     TEXT,
            year        INT,
            fuel_type   INT,
            base_price  NUMERIC,
            image_url   TEXT)
        LANGUAGE sql AS $fn$
            SELECT m.id, m.brand_id, b.name AS brand_name,
                   m.name, m.version, m.year, m.fuel_type::INT,
                   m.base_price, m.image_url
            FROM vehicle_models m
            JOIN vehicle_brands b ON b.id = m.brand_id
            WHERE m.id = p_id;
        $fn$;
        """;

    public const string CreateVehicleBrand = """
        CREATE OR REPLACE FUNCTION fn_create_vehicle_brand(p_name TEXT, p_logo_url TEXT DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE v_id UUID := gen_random_uuid(); v_now TIMESTAMPTZ := NOW();
        BEGIN
            INSERT INTO vehicle_brands (id, name, logo_url, is_active, created_at, updated_at)
            VALUES (v_id, p_name, p_logo_url, TRUE, v_now, v_now);
            RETURN v_id;
        END;
        $fn$;
        """;

    public const string DeleteVehicleBrand = """
        CREATE OR REPLACE FUNCTION fn_delete_vehicle_brand(p_id UUID)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            DELETE FROM vehicle_brands WHERE id = p_id;
        END;
        $fn$;
        """;

    public const string CreateVehicleModel = """
        CREATE OR REPLACE FUNCTION fn_create_vehicle_model(
            p_brand_id  UUID,
            p_name      TEXT,
            p_version   TEXT    DEFAULT NULL,
            p_year      INT     DEFAULT NULL,
            p_fuel_type INT     DEFAULT NULL,
            p_base_price NUMERIC DEFAULT NULL,
            p_image_url TEXT    DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE v_id UUID := gen_random_uuid(); v_now TIMESTAMPTZ := NOW();
        BEGIN
            INSERT INTO vehicle_models (
                id, brand_id, name, version, year, fuel_type, base_price, image_url,
                is_active, created_at, updated_at)
            VALUES (v_id, p_brand_id, p_name, p_version, p_year, p_fuel_type, p_base_price, p_image_url,
                    TRUE, v_now, v_now);
            RETURN v_id;
        END;
        $fn$;
        """;

    public const string UpdateVehicleModel = """
        CREATE OR REPLACE FUNCTION fn_update_vehicle_model(
            p_id        UUID,
            p_name      TEXT,
            p_version   TEXT    DEFAULT NULL,
            p_year      INT     DEFAULT NULL,
            p_fuel_type INT     DEFAULT NULL,
            p_base_price NUMERIC DEFAULT NULL,
            p_image_url TEXT    DEFAULT NULL)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE vehicle_models SET
                name       = p_name,
                version    = p_version,
                year       = p_year,
                fuel_type  = p_fuel_type,
                base_price = p_base_price,
                image_url  = p_image_url,
                updated_at = NOW()
            WHERE id = p_id;
        END;
        $fn$;
        """;

    public const string DeleteVehicleModel = """
        CREATE OR REPLACE FUNCTION fn_delete_vehicle_model(p_id UUID)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN DELETE FROM vehicle_models WHERE id = p_id; END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // BRANDS (company brands)
    // ════════════════════════════════════════════════════════════════════════

    public const string GetBrandsByCompany = """
        CREATE OR REPLACE FUNCTION fn_get_brands_by_company(p_company_id UUID)
        RETURNS TABLE (
            id            UUID,
            name          TEXT,
            primary_color TEXT,
            is_active     BOOLEAN,
            user_count    BIGINT)
        LANGUAGE sql AS $fn$
            SELECT b.id, b.name, b.primary_color, b.is_active,
                   COUNT(u.id) AS user_count
            FROM brands b
            LEFT JOIN users u ON u.brand_id = b.id
            WHERE b.company_id = p_company_id
            GROUP BY b.id, b.name, b.primary_color, b.is_active
            ORDER BY b.name;
        $fn$;
        """;

    public const string GetBrandById = """
        CREATE OR REPLACE FUNCTION fn_get_brand_by_id(p_id UUID, p_company_id UUID)
        RETURNS TABLE (
            id          UUID, company_id UUID,
            name        TEXT, description TEXT,
            phone       TEXT, email       TEXT,
            address     TEXT, logo_url    TEXT,
            primary_color TEXT, is_active BOOLEAN,
            created_at  TIMESTAMPTZ)
        LANGUAGE sql AS $fn$
            SELECT id, company_id, name, description, phone, email,
                   address, logo_url, primary_color, is_active, created_at
            FROM brands WHERE id = p_id AND company_id = p_company_id;
        $fn$;
        """;

    public const string CreateBrand = """
        CREATE OR REPLACE FUNCTION fn_create_brand(
            p_company_id  UUID,
            p_name        TEXT,
            p_description TEXT    DEFAULT NULL,
            p_phone       TEXT    DEFAULT NULL,
            p_email       TEXT    DEFAULT NULL,
            p_address     TEXT    DEFAULT NULL,
            p_logo_url    TEXT    DEFAULT NULL,
            p_color       TEXT    DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE v_id UUID := gen_random_uuid(); v_now TIMESTAMPTZ := NOW();
        BEGIN
            INSERT INTO brands (
                id, company_id, name, description, phone, email,
                address, logo_url, primary_color, is_active, created_at, updated_at)
            VALUES (v_id, p_company_id, p_name, p_description, p_phone, p_email,
                    p_address, p_logo_url, p_color, TRUE, v_now, v_now);
            RETURN v_id;
        END;
        $fn$;
        """;

    public const string UpdateBrand = """
        CREATE OR REPLACE FUNCTION fn_update_brand(
            p_id          UUID,
            p_company_id  UUID,
            p_name        TEXT,
            p_description TEXT    DEFAULT NULL,
            p_phone       TEXT    DEFAULT NULL,
            p_email       TEXT    DEFAULT NULL,
            p_address     TEXT    DEFAULT NULL,
            p_logo_url    TEXT    DEFAULT NULL,
            p_color       TEXT    DEFAULT NULL)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE brands SET
                name        = p_name,
                description = p_description,
                phone       = p_phone,
                email       = p_email,
                address     = p_address,
                logo_url    = p_logo_url,
                primary_color = p_color,
                updated_at  = NOW()
            WHERE id = p_id AND company_id = p_company_id;
        END;
        $fn$;
        """;

    public const string SetBrandActive = """
        CREATE OR REPLACE FUNCTION fn_set_brand_active(p_id UUID, p_is_active BOOLEAN)
        RETURNS VOID LANGUAGE plpgsql AS $fn$
        BEGIN
            UPDATE brands SET is_active = p_is_active, updated_at = NOW() WHERE id = p_id;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // AUTH
    // ════════════════════════════════════════════════════════════════════════

    public const string EmailExists = """
        CREATE OR REPLACE FUNCTION fn_email_exists(p_email TEXT)
        RETURNS BOOLEAN LANGUAGE sql AS $fn$
            SELECT EXISTS (SELECT 1 FROM users WHERE email = LOWER(p_email));
        $fn$;
        """;

    public const string FindUserByEmail = """
        CREATE OR REPLACE FUNCTION fn_find_user_by_email(p_email TEXT)
        RETURNS TABLE (
            id             UUID,
            company_id     UUID,
            brand_id       UUID,
            full_name      TEXT,
            email          TEXT,
            password_hash  TEXT,
            phone          TEXT,
            role           INT,
            account_status INT,
            is_active      BOOLEAN,
            company_name   TEXT,
            company_is_active BOOLEAN,
            brand_name     TEXT,
            brand_color    TEXT,
            brand_is_active BOOLEAN)
        LANGUAGE sql AS $fn$
            SELECT
                u.id, u.company_id, u.brand_id,
                u.full_name, u.email, u.password_hash, u.phone,
                u.role::INT, u.account_status::INT, u.is_active,
                comp.name, comp.is_active,
                b.name, b.primary_color, b.is_active
            FROM users u
            JOIN companies comp ON comp.id = u.company_id
            JOIN brands    b    ON b.id    = u.brand_id
            WHERE u.email = LOWER(p_email);
        $fn$;
        """;

    /// <summary>
    /// ATOMIC registration for a Manager: creates Company + Brand + User + 7 default stages
    /// (3 with templates) + NotificationPreference in one transaction.
    /// Password must be pre-hashed in C#.
    /// Returns TABLE(company_id, brand_id, user_id).
    /// </summary>
    public const string RegisterManager = """
        CREATE OR REPLACE FUNCTION fn_register_manager(
            p_company_name TEXT,
            p_brand_name   TEXT,
            p_full_name    TEXT,
            p_email        TEXT,
            p_password_hash TEXT,
            p_phone        TEXT DEFAULT NULL)
        RETURNS TABLE (company_id UUID, brand_id UUID, user_id UUID)
        LANGUAGE plpgsql AS $fn$
        DECLARE
            v_company_id UUID := gen_random_uuid();
            v_brand_id   UUID := gen_random_uuid();
            v_user_id    UUID := gen_random_uuid();
            v_stage_0    UUID := gen_random_uuid();
            v_stage_1    UUID := gen_random_uuid();
            v_stage_2    UUID := gen_random_uuid();
            v_stage_3    UUID := gen_random_uuid();
            v_stage_4    UUID := gen_random_uuid();
            v_stage_5    UUID := gen_random_uuid();
            v_stage_6    UUID := gen_random_uuid();
            v_now        TIMESTAMPTZ := NOW();
        BEGIN
            INSERT INTO companies (id, name, is_active, created_at, updated_at)
            VALUES (v_company_id, p_company_name, TRUE, v_now, v_now);

            INSERT INTO brands (id, company_id, name, is_active, created_at, updated_at)
            VALUES (v_brand_id, v_company_id, p_brand_name, TRUE, v_now, v_now);

            INSERT INTO users (
                id, company_id, brand_id, full_name, email, password_hash, phone,
                role, account_status, is_active, created_at, updated_at)
            VALUES (
                v_user_id, v_company_id, v_brand_id, p_full_name, LOWER(p_email),
                p_password_hash, p_phone,
                1, 1, TRUE, v_now, v_now);

            -- Default stages
            INSERT INTO client_stages (id, user_id, name, color, "order", is_final, is_won, is_lost, is_active, created_at, updated_at)
            VALUES
                (v_stage_0, v_user_id, 'Aguarda Agendamento de Visita', '#94A3B8', 0, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_1, v_user_id, 'Visita Agendada',               '#3B82F6', 1, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_2, v_user_id, 'Agendar Test Drive',            '#8B5CF6', 2, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_3, v_user_id, 'Test Drive Marcado',            '#F59E0B', 3, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_4, v_user_id, 'Aguarda Decisao',               '#EF4444', 4, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_5, v_user_id, 'Venda',                         '#10B981', 5, TRUE,  TRUE,  FALSE, TRUE, v_now, v_now),
                (v_stage_6, v_user_id, 'Perdido',                       '#6B7280', 6, TRUE,  FALSE, TRUE,  TRUE, v_now, v_now);

            -- Templates for stages 1, 4, 5
            INSERT INTO stage_notification_templates (id, stage_id, title, days_after, is_enabled, created_at, updated_at)
            VALUES
                (gen_random_uuid(), v_stage_1, 'Confirmar visita',     1,  TRUE, v_now, v_now),
                (gen_random_uuid(), v_stage_4, 'Ligar ao cliente',     2,  TRUE, v_now, v_now),
                (gen_random_uuid(), v_stage_5, 'Contacto pos-venda',   30, TRUE, v_now, v_now);

            INSERT INTO notification_preferences (
                id, user_id, daily_digest_time, digest_frequency_days, sale_follow_up_days,
                digest_enabled, stage_change_notifications_enabled, sale_notifications_enabled,
                is_active, created_at, updated_at)
            VALUES (
                gen_random_uuid(), v_user_id,
                '09:29:00'::TIME, 2, 30,
                TRUE, TRUE, TRUE, TRUE, v_now, v_now);

            RETURN QUERY SELECT v_company_id, v_brand_id, v_user_id;
        END;
        $fn$;
        """;

    /// <summary>
    /// ATOMIC registration for a Salesperson: creates User + 7 default stages + NotificationPreference.
    /// Returns the new user_id.
    /// </summary>
    public const string RegisterSalesperson = """
        CREATE OR REPLACE FUNCTION fn_register_salesperson(
            p_company_id    UUID,
            p_brand_id      UUID,
            p_full_name     TEXT,
            p_email         TEXT,
            p_password_hash TEXT,
            p_phone         TEXT DEFAULT NULL)
        RETURNS UUID LANGUAGE plpgsql AS $fn$
        DECLARE
            v_user_id UUID := gen_random_uuid();
            v_stage_0 UUID := gen_random_uuid();
            v_stage_1 UUID := gen_random_uuid();
            v_stage_2 UUID := gen_random_uuid();
            v_stage_3 UUID := gen_random_uuid();
            v_stage_4 UUID := gen_random_uuid();
            v_stage_5 UUID := gen_random_uuid();
            v_stage_6 UUID := gen_random_uuid();
            v_now     TIMESTAMPTZ := NOW();
        BEGIN
            INSERT INTO users (
                id, company_id, brand_id, full_name, email, password_hash, phone,
                role, account_status, is_active, created_at, updated_at)
            VALUES (
                v_user_id, p_company_id, p_brand_id, p_full_name, LOWER(p_email),
                p_password_hash, p_phone,
                0, 1, TRUE, v_now, v_now);

            INSERT INTO client_stages (id, user_id, name, color, "order", is_final, is_won, is_lost, is_active, created_at, updated_at)
            VALUES
                (v_stage_0, v_user_id, 'Aguarda Agendamento de Visita', '#94A3B8', 0, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_1, v_user_id, 'Visita Agendada',               '#3B82F6', 1, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_2, v_user_id, 'Agendar Test Drive',            '#8B5CF6', 2, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_3, v_user_id, 'Test Drive Marcado',            '#F59E0B', 3, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_4, v_user_id, 'Aguarda Decisao',               '#EF4444', 4, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
                (v_stage_5, v_user_id, 'Venda',                         '#10B981', 5, TRUE,  TRUE,  FALSE, TRUE, v_now, v_now),
                (v_stage_6, v_user_id, 'Perdido',                       '#6B7280', 6, TRUE,  FALSE, TRUE,  TRUE, v_now, v_now);

            INSERT INTO stage_notification_templates (id, stage_id, title, days_after, is_enabled, created_at, updated_at)
            VALUES
                (gen_random_uuid(), v_stage_1, 'Confirmar visita',   1,  TRUE, v_now, v_now),
                (gen_random_uuid(), v_stage_4, 'Ligar ao cliente',   2,  TRUE, v_now, v_now),
                (gen_random_uuid(), v_stage_5, 'Contacto pos-venda', 30, TRUE, v_now, v_now);

            INSERT INTO notification_preferences (
                id, user_id, daily_digest_time, digest_frequency_days, sale_follow_up_days,
                digest_enabled, stage_change_notifications_enabled, sale_notifications_enabled,
                is_active, created_at, updated_at)
            VALUES (
                gen_random_uuid(), v_user_id,
                '09:29:00'::TIME, 2, 30,
                TRUE, TRUE, TRUE, TRUE, v_now, v_now);

            RETURN v_user_id;
        END;
        $fn$;
        """;

    // ════════════════════════════════════════════════════════════════════════
    // Registry — ordered list applied at startup
    // ════════════════════════════════════════════════════════════════════════

    public static readonly IReadOnlyList<string> All =
    [
        // Clients
        GetClientsPaged,
        GetClientById,
        GetClientHistory,
        GetClientSalesHistory,
        CreateClient,
        UpdateClientStage,
        SoftDeleteClient,
        // Proposals
        GetProposalsPaged,
        GetProposalById,
        CreateProposal,
        UpdateProposal,
        MarkProposalLost,
        ConvertProposalToSale,
        // Sales & Dashboard
        GetSalesPaged,
        GetSaleById,
        GetHotDeals,
        GetDashboardKpis,
        GetMonthlyStats,
        GetLossReasons,
        // Notifications
        GetTodayNotifications,
        GetNotificationsPaged,
        GetOverdueCount,
        CreateNotification,
        UpdateNotificationDone,
        UpdateNotificationSnoozed,
        UpdateNotificationIgnored,
        GetNotificationPrefs,
        UpdateNotificationPrefs,
        // Stages
        GetStagesByUser,
        CreateStage,
        UpdateStage,
        DeleteStage,
        ReorderStages,
        CreateStageTemplate,
        UpdateStageTemplate,
        DeleteStageTemplate,
        // Users
        GetUsersByBrand,
        GetUserById,
        UpdateUser,
        UpdateUserActive,
        // Vehicles
        GetVehicleBrands,
        GetVehicleModelsPaged,
        GetVehicleModelById,
        CreateVehicleBrand,
        DeleteVehicleBrand,
        CreateVehicleModel,
        UpdateVehicleModel,
        DeleteVehicleModel,
        // Brands
        GetBrandsByCompany,
        GetBrandById,
        CreateBrand,
        UpdateBrand,
        SetBrandActive,
        // Auth
        EmailExists,
        FindUserByEmail,
        RegisterManager,
        RegisterSalesperson,
    ];
}
