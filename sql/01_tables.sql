-- =============================================================================
-- OnTimeCRM — PostgreSQL Schema DDL
-- =============================================================================
-- All identifiers are lowercase snake_case.
-- EF Core uses UseSnakeCaseNamingConvention() which generates these same names.
-- Run this script once on a fresh database to create all tables.
-- NOTE: "order" is a reserved keyword → must be quoted as "order" in queries.
-- NOTE: "trigger" is a non-reserved keyword → can be used unquoted, but is
--       quoted in the DatabaseFunctions.cs SQL strings for clarity.
--
-- Enum reference (stored as INT):
--   UserRole              : Salesperson=0  Manager=1
--   UserAccountStatus     : PendingActivation=0  Active=1  Expired=2
--                           Suspended=3  Cancelled=5  Inactive=4
--   SubscriptionPlan      : Trial=0  Monthly=1  Annual=2
--   SubscriptionStatus    : Trial=0  Active=1  PastDue=2  Cancelled=3  Expired=4
--   PaymentProvider       : Stripe=0  Ifthenpay=1
--   PaymentMethodType     : Card=0  MBWay=1  Multibanco=2
--   SubscriptionPaymentStatus: Pending=0  Paid=1  Failed=2  Refunded=3  Expired=4
--   LeadSource            : WalkIn=0  OLX=1  StandVirtual=2  Instagram=3
--                           Referral=4  Email=5  Phone=6  Other=7
--   BusinessType          : DirectPurchase=0  Consignment=1  Lease=2
--                           FinancingExternal=3  Financing=4
--   PaymentType           : Cash=0  BankTransfer=1  Financing=2  Leasing=3  Other=4
--   ProposalStatus        : Active=0  Won=1  Lost=2  Cancelled=3
--   LossReason            : Price=0  Competition=1  NoDecision=2
--                           FinancingRefused=3  TimeOut=4  NotInterested=5  Other=6
--   TradeInType           : Car=0  Motorcycle=1  Van=2  Truck=3  Other=4
--   NotificationStatus    : Pending=0  Done=1  Snoozed=2  Ignored=3
--   NotificationTrigger   : Manual=0  StageChanged=1  SaleClosed=2
--                           ProposalCreated=3  Custom=4
--   DealTemperature       : Hot=0  Warm=1  Cold=2
--   FuelType              : Petrol=0  Diesel=1  Electric=2  Hybrid=3
--                           PlugInHybrid=4  LPG=5  Other=6
-- =============================================================================

-- Enable pgcrypto for gen_random_uuid() (needed if PostgreSQL < 13)
-- CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. COMPANIES
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS companies (
    id          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    name        VARCHAR(150) NOT NULL,
    tax_id      VARCHAR(20),
    phone       VARCHAR(30),
    email       VARCHAR(254),
    website     VARCHAR(200),
    address     VARCHAR(300),
    logo_url    VARCHAR(500),
    is_active   BOOLEAN     NOT NULL DEFAULT TRUE,
    notes       VARCHAR(1000),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. BRANDS  (stands / dealerships within a company)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS brands (
    id            UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    company_id    UUID        NOT NULL REFERENCES companies(id) ON DELETE RESTRICT,
    name          VARCHAR(150) NOT NULL,
    description   TEXT,
    phone         VARCHAR(30),
    email         VARCHAR(254),
    address       VARCHAR(300),
    logo_url      VARCHAR(500),
    primary_color VARCHAR(7),           -- hex e.g. "#1C69D4"
    is_active     BOOLEAN     NOT NULL DEFAULT TRUE,
    notes         VARCHAR(1000),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_brands_company_id ON brands(company_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. USERS
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS users (
    id                       UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    company_id               UUID        NOT NULL REFERENCES companies(id) ON DELETE RESTRICT,
    brand_id                 UUID        NOT NULL REFERENCES brands(id) ON DELETE RESTRICT,
    full_name                VARCHAR(150) NOT NULL,
    email                    VARCHAR(254) NOT NULL,
    password_hash            TEXT        NOT NULL,
    phone                    VARCHAR(30),
    -- UserRole: 0=Salesperson, 1=Manager
    role                     SMALLINT    NOT NULL DEFAULT 0,
    is_email_verified        BOOLEAN     NOT NULL DEFAULT FALSE,
    last_login_at            TIMESTAMPTZ,
    -- UserAccountStatus: 0=PendingActivation, 1=Active, 2=Expired, 3=Suspended, 4=Inactive, 5=Cancelled
    account_status           SMALLINT    NOT NULL DEFAULT 0,
    -- SubscriptionPlan: 0=Trial, 1=Monthly, 2=Annual
    plan                     SMALLINT    NOT NULL DEFAULT 0,
    -- SubscriptionStatus: 0=Trial, 1=Active, 2=PastDue, 3=Cancelled, 4=Expired
    subscription_status      SMALLINT    NOT NULL DEFAULT 0,
    trial_ends_at            TIMESTAMPTZ,
    subscription_started_at  TIMESTAMPTZ,
    subscription_expires_at  TIMESTAMPTZ,
    subscription_cancelled_at TIMESTAMPTZ,
    grace_period_days        INT         NOT NULL DEFAULT 3,
    -- Stripe
    stripe_customer_id       VARCHAR(100),
    stripe_subscription_id   VARCHAR(100),
    is_active                BOOLEAN     NOT NULL DEFAULT TRUE,
    notes                    VARCHAR(1000),
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_users_email     ON users(email);
CREATE INDEX        IF NOT EXISTS ix_users_brand_id  ON users(brand_id);
CREATE INDEX        IF NOT EXISTS ix_users_company_id ON users(company_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. USER SUBSCRIPTION PAYMENTS
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS user_subscription_payments (
    id                      UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    user_id                 UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    -- SubscriptionPlan: 0=Trial, 1=Monthly, 2=Annual
    plan                    SMALLINT    NOT NULL,
    -- SubscriptionPaymentStatus: 0=Pending, 1=Paid, 2=Failed, 3=Refunded, 4=Expired
    status                  SMALLINT    NOT NULL DEFAULT 0,
    -- PaymentProvider: 0=Stripe, 1=Ifthenpay
    provider                SMALLINT    NOT NULL,
    -- PaymentMethodType: 0=Card, 1=MBWay, 2=Multibanco
    payment_method          SMALLINT    NOT NULL,
    amount                  NUMERIC(18,2) NOT NULL,
    currency                VARCHAR(3)  NOT NULL DEFAULT 'EUR',
    period_start            TIMESTAMPTZ NOT NULL,
    period_end              TIMESTAMPTZ NOT NULL,
    paid_at                 TIMESTAMPTZ,
    failed_at               TIMESTAMPTZ,
    expires_at              TIMESTAMPTZ,
    -- Stripe
    stripe_payment_intent_id VARCHAR(200),
    stripe_invoice_id        VARCHAR(200),
    -- Ifthenpay
    ifthenpay_reference      VARCHAR(50),
    ifthenpay_mb_way_alias   VARCHAR(50),
    ifthenpay_transaction_id VARCHAR(100),
    failure_reason           VARCHAR(500),
    is_active                BOOLEAN     NOT NULL DEFAULT TRUE,
    notes                    VARCHAR(1000),
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_user_subscription_payments_user_id ON user_subscription_payments(user_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. VEHICLE BRANDS  (globally shared catalogue)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS vehicle_brands (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    name       VARCHAR(100) NOT NULL,
    logo_url   VARCHAR(500),
    is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
    notes      VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_vehicle_brands_name ON vehicle_brands(name);

-- ─────────────────────────────────────────────────────────────────────────────
-- 6. VEHICLE MODELS  (globally shared catalogue)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS vehicle_models (
    id         UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    brand_id   UUID         NOT NULL REFERENCES vehicle_brands(id) ON DELETE RESTRICT,
    name       VARCHAR(100) NOT NULL,
    version    VARCHAR(100),
    year       INT,
    -- FuelType: 0=Petrol, 1=Diesel, 2=Electric, 3=Hybrid, 4=PlugInHybrid, 5=LPG, 6=Other
    fuel_type  SMALLINT,
    base_price NUMERIC(18,2),
    image_url  VARCHAR(500),
    is_active  BOOLEAN      NOT NULL DEFAULT TRUE,
    notes      VARCHAR(1000),
    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_vehicle_models_brand_id ON vehicle_models(brand_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 7. CLIENT STAGES  (configurable pipeline stages per user)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS client_stages (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    user_id    UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name       VARCHAR(100) NOT NULL,
    color      VARCHAR(7),             -- hex e.g. "#3B82F6"
    "order"    INT         NOT NULL DEFAULT 0,
    is_final   BOOLEAN     NOT NULL DEFAULT FALSE,
    is_won     BOOLEAN     NOT NULL DEFAULT FALSE,
    is_lost    BOOLEAN     NOT NULL DEFAULT FALSE,
    is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
    notes      VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_client_stages_user_id ON client_stages(user_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 8. STAGE NOTIFICATION TEMPLATES
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS stage_notification_templates (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    stage_id   UUID        NOT NULL REFERENCES client_stages(id) ON DELETE CASCADE,
    user_id    UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title      VARCHAR(200) NOT NULL,
    days_after INT         NOT NULL DEFAULT 1,
    is_enabled BOOLEAN     NOT NULL DEFAULT TRUE,
    is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
    notes      VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_stage_notification_templates_stage_id ON stage_notification_templates(stage_id);
CREATE INDEX IF NOT EXISTS ix_stage_notification_templates_user_id  ON stage_notification_templates(user_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 9. NOTIFICATION PREFERENCES  (one per user)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS notification_preferences (
    id                                   UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    user_id                              UUID        NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    daily_digest_time                    TIME        NOT NULL DEFAULT '09:29:00',
    digest_frequency_days                INT         NOT NULL DEFAULT 2,
    sale_follow_up_days                  INT         NOT NULL DEFAULT 30,
    digest_enabled                       BOOLEAN     NOT NULL DEFAULT TRUE,
    stage_change_notifications_enabled   BOOLEAN     NOT NULL DEFAULT TRUE,
    sale_notifications_enabled           BOOLEAN     NOT NULL DEFAULT TRUE,
    is_active                            BOOLEAN     NOT NULL DEFAULT TRUE,
    notes                                VARCHAR(1000),
    created_at                           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─────────────────────────────────────────────────────────────────────────────
-- 10. CLIENTS
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS clients (
    id                  UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    user_id             UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    full_name           VARCHAR(150) NOT NULL,
    email               VARCHAR(254),
    phone               VARCHAR(30),
    tax_id              VARCHAR(20),
    -- LeadSource: 0=WalkIn, 1=OLX, 2=StandVirtual, 3=Instagram, 4=Referral, 5=Email, 6=Phone, 7=Other
    lead_source         SMALLINT    NOT NULL DEFAULT 0,
    current_stage_id    UUID        NOT NULL REFERENCES client_stages(id) ON DELETE RESTRICT,
    -- DealTemperature: 0=Hot, 1=Warm, 2=Cold
    temperature         SMALLINT    NOT NULL DEFAULT 1,
    last_interaction_at TIMESTAMPTZ,
    is_active           BOOLEAN     NOT NULL DEFAULT TRUE,
    notes               VARCHAR(1000),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_clients_user_id          ON clients(user_id);
CREATE INDEX IF NOT EXISTS ix_clients_current_stage_id ON clients(current_stage_id);
CREATE INDEX IF NOT EXISTS ix_clients_temperature      ON clients(temperature) WHERE is_active = TRUE;
-- For dashboard hot deals query performance:
CREATE INDEX IF NOT EXISTS ix_clients_last_interaction ON clients(last_interaction_at DESC) WHERE is_active = TRUE;

-- ─────────────────────────────────────────────────────────────────────────────
-- 11. CLIENT STAGE HISTORIES  (immutable audit trail)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS client_stage_histories (
    id                UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    client_id         UUID        NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    user_id           UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    from_stage_id     UUID        REFERENCES client_stages(id) ON DELETE RESTRICT,   -- NULL = first entry
    to_stage_id       UUID        NOT NULL REFERENCES client_stages(id) ON DELETE RESTRICT,
    obs               VARCHAR(1000),
    -- Abbreviated JSON snapshot: { pid, pd, bt, pt, val, disc, tradeIn, ti, vehicles }
    proposal_snapshot VARCHAR(4000),
    is_active         BOOLEAN     NOT NULL DEFAULT TRUE,
    notes             VARCHAR(1000),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_client_stage_histories_client_id   ON client_stage_histories(client_id);
CREATE INDEX IF NOT EXISTS ix_client_stage_histories_created_at  ON client_stage_histories(created_at DESC);

-- ─────────────────────────────────────────────────────────────────────────────
-- 12. PROPOSALS
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS proposals (
    id                      UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    client_id               UUID          NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
    user_id                 UUID          NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    -- ProposalStatus: 0=Active, 1=Won, 2=Lost, 3=Cancelled
    status                  SMALLINT      NOT NULL DEFAULT 0,
    -- BusinessType: 0=DirectPurchase, 1=Consignment, 2=Lease, 3=FinancingExternal, 4=Financing
    business_type           SMALLINT      NOT NULL DEFAULT 0,
    -- PaymentType: 0=Cash, 1=BankTransfer, 2=Financing, 3=Leasing, 4=Other
    payment_type            SMALLINT      NOT NULL DEFAULT 0,
    proposal_value          NUMERIC(18,2),
    discount                NUMERIC(18,2),
    -- User-controlled business date when proposal was presented (NOT a system timestamp)
    proposal_date           TIMESTAMPTZ,
    -- LossReason: 0=Price, 1=Competition, 2=NoDecision, 3=FinancingRefused, 4=TimeOut, 5=NotInterested, 6=Other
    loss_reason             SMALLINT,
    loss_notes              VARCHAR(1000),
    won_at                  TIMESTAMPTZ,
    lost_at                 TIMESTAMPTZ,
    -- Trade-in
    has_trade_in            BOOLEAN       NOT NULL DEFAULT FALSE,
    -- TradeInType: 0=Car, 1=Motorcycle, 2=Van, 3=Truck, 4=Other
    trade_in_type           SMALLINT,
    trade_in_plate          VARCHAR(20),
    trade_in_brand          VARCHAR(100),
    trade_in_model          VARCHAR(100),
    trade_in_year           INT,
    trade_in_km             INT,
    trade_in_estimated_value NUMERIC(18,2),
    is_active               BOOLEAN       NOT NULL DEFAULT TRUE,
    notes                   VARCHAR(1000),
    created_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_proposals_client_id    ON proposals(client_id);
CREATE INDEX IF NOT EXISTS ix_proposals_user_id      ON proposals(user_id);
CREATE INDEX IF NOT EXISTS ix_proposals_status       ON proposals(status);
-- Dashboard and KPI filters use proposal_date (business date):
CREATE INDEX IF NOT EXISTS ix_proposals_proposal_date ON proposals(proposal_date) WHERE proposal_date IS NOT NULL;

-- ─────────────────────────────────────────────────────────────────────────────
-- 13. PROPOSAL VEHICLES
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS proposal_vehicles (
    id              UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    proposal_id     UUID        NOT NULL REFERENCES proposals(id) ON DELETE CASCADE,
    model_id        UUID        REFERENCES vehicle_models(id) ON DELETE RESTRICT,  -- NULL if free text
    free_text_model VARCHAR(200),
    is_preferred    BOOLEAN     NOT NULL DEFAULT FALSE,
    obs             VARCHAR(1000),
    is_active       BOOLEAN     NOT NULL DEFAULT TRUE,
    notes           VARCHAR(1000),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_proposal_vehicles_proposal_id ON proposal_vehicles(proposal_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- 14. SALES
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sales (
    id              UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    proposal_id     UUID          NOT NULL REFERENCES proposals(id) ON DELETE RESTRICT,
    client_id       UUID          NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    user_id         UUID          NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    model_id        UUID          REFERENCES vehicle_models(id) ON DELETE RESTRICT,  -- nullable
    free_text_model VARCHAR(200),
    final_value     NUMERIC(18,2) NOT NULL,
    -- PaymentType: 0=Cash, 1=BankTransfer, 2=Financing, 3=Leasing, 4=Other
    payment_type    SMALLINT      NOT NULL,
    -- CRITICAL: sold_at is a user-provided business date — NEVER auto-set to NOW()
    sold_at         TIMESTAMPTZ   NOT NULL,
    plate           VARCHAR(20),
    chassis         VARCHAR(50),
    obs             VARCHAR(1000),
    is_active       BOOLEAN       NOT NULL DEFAULT TRUE,
    notes           VARCHAR(1000),
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_sales_user_id     ON sales(user_id);
CREATE INDEX IF NOT EXISTS ix_sales_client_id   ON sales(client_id);
-- Dashboard KPIs filter by sold_at (business date):
CREATE INDEX IF NOT EXISTS ix_sales_sold_at     ON sales(sold_at DESC);

-- ─────────────────────────────────────────────────────────────────────────────
-- 15. NOTIFICATIONS
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS notifications (
    id            UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    user_id       UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    client_id     UUID        REFERENCES clients(id) ON DELETE RESTRICT,
    proposal_id   UUID        REFERENCES proposals(id) ON DELETE RESTRICT,
    sale_id       UUID        REFERENCES sales(id) ON DELETE RESTRICT,
    -- NotificationTrigger: 0=Manual, 1=StageChanged, 2=SaleClosed, 3=ProposalCreated, 4=Custom
    trigger       SMALLINT    NOT NULL DEFAULT 0,
    -- NotificationStatus: 0=Pending, 1=Done, 2=Snoozed, 3=Ignored
    status        SMALLINT    NOT NULL DEFAULT 0,
    title         VARCHAR(200) NOT NULL,
    body          VARCHAR(1000),
    scheduled_for TIMESTAMPTZ NOT NULL,
    done_at       TIMESTAMPTZ,
    snoozed_until TIMESTAMPTZ,
    is_active     BOOLEAN     NOT NULL DEFAULT TRUE,
    notes         VARCHAR(1000),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_notifications_user_id       ON notifications(user_id);
-- Today's and overdue notifications query:
CREATE INDEX IF NOT EXISTS ix_notifications_pending_sched ON notifications(user_id, scheduled_for)
    WHERE status = 0;

-- ─────────────────────────────────────────────────────────────────────────────
-- 16. TRANSLATION ENTRIES  (i18n key-value store)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS translation_entries (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    key        VARCHAR(200) NOT NULL,
    locale     VARCHAR(10)  NOT NULL DEFAULT 'pt-PT',
    value      VARCHAR(500) NOT NULL,
    is_active  BOOLEAN     NOT NULL DEFAULT TRUE,
    notes      VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_translation_entries_key_locale ON translation_entries(key, locale);

-- =============================================================================
-- End of schema
-- =============================================================================
