using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <inheritdoc />
    public partial class AddConsignmentInvoiceReferencesAndManifests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- 1. Create table consignment_invoice_references for customer paper bill intake
                CREATE TABLE IF NOT EXISTS consignment_invoice_references (
                    id bigserial PRIMARY KEY,
                    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                    shipment_id bigint NOT NULL REFERENCES shipments(id) ON DELETE CASCADE,
                    customer_invoice_no character varying(100) NOT NULL,
                    customer_invoice_date date NOT NULL,
                    declared_goods_value numeric(14,2) NOT NULL DEFAULT 0,
                    eway_bill_no character varying(100),
                    eway_bill_date date,
                    eway_bill_valid_upto timestamp without time zone,
                    document_type character varying(50) DEFAULT 'TaxInvoice',
                    package_count integer DEFAULT 1,
                    weight_kg numeric(12,2) DEFAULT 0,
                    commodity_description character varying(500),
                    document_url text,
                    is_active boolean DEFAULT true,
                    created_by integer REFERENCES users(id) ON DELETE SET NULL,
                    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ix_consignment_invoice_references_tenant_id_shipment_id 
                    ON consignment_invoice_references(tenant_id, shipment_id);
                CREATE INDEX IF NOT EXISTS ix_consignment_invoice_references_tenant_id_customer_invoice_no 
                    ON consignment_invoice_references(tenant_id, customer_invoice_no);

                -- 2. Create table manifests (Lorry / Truck Route Manifests)
                CREATE TABLE IF NOT EXISTS manifests (
                    id bigserial PRIMARY KEY,
                    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                    manifest_no character varying(50) NOT NULL,
                    manifest_date date NOT NULL,
                    origin_hub_id bigint REFERENCES locations(id) ON DELETE RESTRICT,
                    destination_hub_id bigint REFERENCES locations(id) ON DELETE RESTRICT,
                    assigned_trip_id bigint REFERENCES trips(id) ON DELETE SET NULL,
                    vehicle_id integer REFERENCES vehicles(id) ON DELETE SET NULL,
                    vehicle_no character varying(50),
                    driver_id integer REFERENCES drivers(id) ON DELETE SET NULL,
                    driver_name character varying(100),
                    driver_mobile character varying(20),
                    total_consignments integer NOT NULL DEFAULT 0,
                    total_packages integer NOT NULL DEFAULT 0,
                    total_weight_kg numeric(12,2) NOT NULL DEFAULT 0,
                    total_freight_to_pay numeric(14,2) NOT NULL DEFAULT 0,
                    total_freight_paid numeric(14,2) NOT NULL DEFAULT 0,
                    total_freight_tbb numeric(14,2) NOT NULL DEFAULT 0,
                    status integer NOT NULL DEFAULT 0,
                    notes text,
                    created_by integer REFERENCES users(id) ON DELETE SET NULL,
                    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
                    updated_at timestamp without time zone
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_manifests_tenant_id_manifest_no 
                    ON manifests(tenant_id, manifest_no);

                -- 3. Create table manifest_items (Bilties attached to a Manifest)
                CREATE TABLE IF NOT EXISTS manifest_items (
                    id bigserial PRIMARY KEY,
                    tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                    manifest_id bigint NOT NULL REFERENCES manifests(id) ON DELETE CASCADE,
                    shipment_id bigint NOT NULL REFERENCES shipments(id) ON DELETE CASCADE,
                    destination_hub_id bigint REFERENCES locations(id) ON DELETE SET NULL,
                    loaded_packages integer NOT NULL DEFAULT 1,
                    loaded_weight_kg numeric(12,2) NOT NULL DEFAULT 0,
                    freight_amount numeric(14,2) NOT NULL DEFAULT 0,
                    payment_term integer NOT NULL DEFAULT 0,
                    delivery_type character varying(50),
                    is_delivered boolean DEFAULT false,
                    delivery_date timestamp without time zone,
                    remarks character varying(500),
                    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS ix_manifest_items_tenant_id_manifest_id 
                    ON manifest_items(tenant_id, manifest_id);
                CREATE INDEX IF NOT EXISTS ix_manifest_items_tenant_id_shipment_id 
                    ON manifest_items(tenant_id, shipment_id);

                -- 4. Ensure hub routing columns exist on shipments
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'origin_hub_id') THEN
                        ALTER TABLE shipments ADD COLUMN origin_hub_id bigint REFERENCES locations(id) ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'destination_hub_id') THEN
                        ALTER TABLE shipments ADD COLUMN destination_hub_id bigint REFERENCES locations(id) ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'current_hub_id') THEN
                        ALTER TABLE shipments ADD COLUMN current_hub_id bigint REFERENCES locations(id) ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'delivery_type') THEN
                        ALTER TABLE shipments ADD COLUMN delivery_type character varying(50) DEFAULT 'Godown';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'eway_bill_no') THEN
                        ALTER TABLE shipments ADD COLUMN eway_bill_no character varying(100);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'shipments' AND column_name = 'eway_bill_valid_upto') THEN
                        ALTER TABLE shipments ADD COLUMN eway_bill_valid_upto timestamp without time zone;
                    END IF;
                END $$;

                -- 5. Ensure manifest columns exist on trips
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'manifest_id') THEN
                        ALTER TABLE trips ADD COLUMN manifest_id bigint REFERENCES manifests(id) ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'route_name') THEN
                        ALTER TABLE trips ADD COLUMN route_name character varying(200);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'origin_hub_id') THEN
                        ALTER TABLE trips ADD COLUMN origin_hub_id bigint REFERENCES locations(id) ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trips' AND column_name = 'destination_hub_id') THEN
                        ALTER TABLE trips ADD COLUMN destination_hub_id bigint REFERENCES locations(id) ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS manifest_items;
                DROP TABLE IF EXISTS manifests;
                DROP TABLE IF EXISTS consignment_invoice_references;
            ");
        }
    }
}
