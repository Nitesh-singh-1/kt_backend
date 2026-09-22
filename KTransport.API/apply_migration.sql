-- 1. Create subscription_plans table
CREATE TABLE IF NOT EXISTS subscription_plans (
    id uuid PRIMARY KEY,
    name character varying(100) NOT NULL,
    tier character varying(50) NOT NULL,
    description character varying(500) NOT NULL,
    max_vehicles integer NOT NULL,
    max_users integer NOT NULL,
    max_monthly_shipments integer NOT NULL,
    storage_limit_mb integer NOT NULL,
    monthly_price numeric(14,2) NOT NULL,
    is_active boolean DEFAULT true,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);

-- 2. Create tenant_settings table
CREATE TABLE IF NOT EXISTS tenant_settings (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL UNIQUE REFERENCES tenants(id) ON DELETE CASCADE,
    general_json text NOT NULL,
    billing_and_tax_json text NOT NULL,
    document_sequences_json text NOT NULL,
    operational_workflows_json text NOT NULL,
    feature_flags_json text NOT NULL,
    integrations_json text NOT NULL,
    custom_settings_json text NOT NULL,
    menu_entitlements_json text NOT NULL DEFAULT '{}',
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone
);

-- Ensure menu_entitlements_json column exists
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'tenant_settings' AND column_name = 'menu_entitlements_json') THEN
        ALTER TABLE tenant_settings ADD COLUMN menu_entitlements_json text NOT NULL DEFAULT '{}';
    END IF;
END $$;

-- 3. Create tenant_subscriptions table
CREATE TABLE IF NOT EXISTS tenant_subscriptions (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    subscription_plan_id uuid NOT NULL REFERENCES subscription_plans(id) ON DELETE CASCADE,
    status character varying(50) NOT NULL DEFAULT 'Active',
    started_at timestamp without time zone NOT NULL,
    expires_at timestamp without time zone,
    is_auto_renew boolean DEFAULT true,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone
);

-- 4. Seed standard subscription plans if empty
INSERT INTO subscription_plans (id, name, tier, description, max_vehicles, max_users, max_monthly_shipments, storage_limit_mb, monthly_price, is_active, created_at)
VALUES 
('11111111-2222-3333-4444-555555555555', 'Starter Tier', 'Starter', 'Single branch transport company up to 10 vehicles', 10, 3, 150, 5120, 999.00, true, CURRENT_TIMESTAMP),
('22222222-2222-3333-4444-555555555555', 'Professional Tier', 'Professional', 'Regional transport fleet up to 50 vehicles with GPS telematics', 50, 15, 1000, 10240, 3999.00, true, CURRENT_TIMESTAMP),
('33333333-3333-3333-3333-333333333333', 'Enterprise Dedicated Fleet Tier', 'Enterprise', 'Full scale national logistics operations with unlimited scalability', 500, 100, 10000, 51200, 9999.00, true, CURRENT_TIMESTAMP)
ON CONFLICT (id) DO NOTHING;

-- 5. Seed default tenant subscription for Default Organization
INSERT INTO tenant_subscriptions (id, tenant_id, subscription_plan_id, status, started_at, expires_at, is_auto_renew, created_at)
SELECT '44444444-5555-6666-7777-888888888888', '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333', 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '5 years', true, CURRENT_TIMESTAMP
WHERE EXISTS (SELECT 1 FROM tenants WHERE id = '11111111-1111-1111-1111-111111111111')
ON CONFLICT (id) DO NOTHING;

-- 6. Record in __EFMigrationsHistory
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260922205500_AddSaaSConfigurationAndSubscription', '8.0.0')
ON CONFLICT DO NOTHING;
