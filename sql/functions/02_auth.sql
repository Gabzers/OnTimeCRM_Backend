-- =============================================================================
-- OnTimeCRM — PostgreSQL Functions: AUTH
-- =============================================================================
-- fn_email_exists         — fast email uniqueness check
-- fn_find_user_by_email   — load user (with company + brand) by email
-- fn_register_manager     — ATOMIC: create company + brand + manager + seed data
-- fn_register_salesperson — ATOMIC: create salesperson + seed data
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_email_exists
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_email_exists(p_email TEXT)
RETURNS BOOLEAN
LANGUAGE sql AS $fn$
    SELECT EXISTS (SELECT 1 FROM users WHERE email = LOWER(p_email));
$fn$;

-- ---------------------------------------------------------------------------
-- fn_find_user_by_email
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_find_user_by_email(p_email TEXT)
RETURNS TABLE (
    id                UUID,
    company_id        UUID,
    brand_id          UUID,
    full_name         TEXT,
    email             TEXT,
    password_hash     TEXT,
    phone             TEXT,
    role              INT,
    account_status    INT,
    is_active         BOOLEAN,
    company_name      TEXT,
    company_is_active BOOLEAN,
    brand_name        TEXT,
    brand_color       TEXT,
    brand_is_active   BOOLEAN)
LANGUAGE sql AS $fn$
    SELECT
        u.id, u.company_id, u.brand_id,
        u.full_name, u.email, u.password_hash, u.phone,
        u.role::INT, u.account_status::INT, u.is_active,
        comp.name,  comp.is_active,
        b.name, b.primary_color, b.is_active
    FROM users u
    JOIN companies comp ON comp.id = u.company_id
    JOIN brands    b    ON b.id    = u.brand_id
    WHERE u.email = LOWER(p_email);
$fn$;

-- ---------------------------------------------------------------------------
-- fn_register_manager
-- ATOMIC: company + brand + manager user + 7 default stages (3 with templates)
--         + notification_preference — all in one transaction.
-- Password must be pre-hashed in C# (PBKDF2).
-- Returns TABLE(company_id, brand_id, user_id).
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_register_manager(
    p_company_name  TEXT,
    p_brand_name    TEXT,
    p_full_name     TEXT,
    p_email         TEXT,
    p_password_hash TEXT,
    p_phone         TEXT DEFAULT NULL)
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

    -- role=1 (Manager), account_status=1 (Active)
    INSERT INTO users (
        id, company_id, brand_id, full_name, email, password_hash, phone,
        role, account_status, is_active, created_at, updated_at)
    VALUES (
        v_user_id, v_company_id, v_brand_id, p_full_name, LOWER(p_email),
        p_password_hash, p_phone,
        1, 1, TRUE, v_now, v_now);

    -- Default 7 stages
    INSERT INTO client_stages (id, user_id, name, color, "order", is_final, is_won, is_lost, is_active, created_at, updated_at)
    VALUES
        (v_stage_0, v_user_id, 'Aguarda Agendamento de Visita', '#94A3B8', 0, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
        (v_stage_1, v_user_id, 'Visita Agendada',               '#3B82F6', 1, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
        (v_stage_2, v_user_id, 'Agendar Test Drive',            '#8B5CF6', 2, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
        (v_stage_3, v_user_id, 'Test Drive Marcado',            '#F59E0B', 3, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
        (v_stage_4, v_user_id, 'Aguarda Decisao',               '#EF4444', 4, FALSE, FALSE, FALSE, TRUE, v_now, v_now),
        (v_stage_5, v_user_id, 'Venda',                         '#10B981', 5, TRUE,  TRUE,  FALSE, TRUE, v_now, v_now),
        (v_stage_6, v_user_id, 'Perdido',                       '#6B7280', 6, TRUE,  FALSE, TRUE,  TRUE, v_now, v_now);

    -- Notification templates for stages 1 (visit), 4 (decision), 5 (sale)
    INSERT INTO stage_notification_templates (id, stage_id, user_id, title, days_after, is_enabled, created_at, updated_at)
    VALUES
        (gen_random_uuid(), v_stage_1, v_user_id, 'Confirmar visita',   1,  TRUE, v_now, v_now),
        (gen_random_uuid(), v_stage_4, v_user_id, 'Ligar ao cliente',   2,  TRUE, v_now, v_now),
        (gen_random_uuid(), v_stage_5, v_user_id, 'Contacto pos-venda', 30, TRUE, v_now, v_now);

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

-- ---------------------------------------------------------------------------
-- fn_register_salesperson
-- ATOMIC: user + 7 default stages + notification_preference.
-- Returns the new user_id.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_register_salesperson(
    p_company_id    UUID,
    p_brand_id      UUID,
    p_full_name     TEXT,
    p_email         TEXT,
    p_password_hash TEXT,
    p_phone         TEXT DEFAULT NULL)
RETURNS UUID
LANGUAGE plpgsql AS $fn$
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
    -- role=0 (Salesperson), account_status=1 (Active)
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

    INSERT INTO stage_notification_templates (id, stage_id, user_id, title, days_after, is_enabled, created_at, updated_at)
    VALUES
        (gen_random_uuid(), v_stage_1, v_user_id, 'Confirmar visita',   1,  TRUE, v_now, v_now),
        (gen_random_uuid(), v_stage_4, v_user_id, 'Ligar ao cliente',   2,  TRUE, v_now, v_now),
        (gen_random_uuid(), v_stage_5, v_user_id, 'Contacto pos-venda', 30, TRUE, v_now, v_now);

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
