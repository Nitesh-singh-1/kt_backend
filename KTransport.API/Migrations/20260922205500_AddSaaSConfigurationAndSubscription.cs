using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSConfigurationAndSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

                -- Ensure column menu_entitlements_json exists in tenant_settings
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'tenant_settings' AND column_name = 'menu_entitlements_json') THEN
                        ALTER TABLE tenant_settings ADD COLUMN menu_entitlements_json text NOT NULL DEFAULT '{}';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS tenant_settings;
                DROP TABLE IF EXISTS tenant_subscriptions;
                DROP TABLE IF EXISTS subscription_plans;
            ");
        }
    }
}
