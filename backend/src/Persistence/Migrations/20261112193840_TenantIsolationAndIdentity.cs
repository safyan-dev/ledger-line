using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantIsolationAndIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_webhook_events_provider_provider_event_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropIndex(
                name: "ux_reconciliation_records_payment_success",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropIndex(
                name: "ix_payments_provider_provider_payment_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_orders_order_number",
                schema: "orders",
                table: "orders");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "webhooks",
                table: "webhook_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "payments",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "payments",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "audit",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_payments_tenant_id_id",
                schema: "payments",
                table: "payments",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_orders_tenant_id_id",
                schema: "orders",
                table: "orders",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    is_platform_admin = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_users",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_users_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_users_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                schema: "identity",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                "INSERT INTO tenancy.tenants (id, name, slug, status, created_at, updated_at) " +
                "SELECT '33333333-3333-3333-3333-333333333333', 'Ledgerline Demo', 'demo', 'Active', NOW(), NOW() " +
                "WHERE NOT EXISTS (SELECT 1 FROM tenancy.tenants WHERE slug = 'demo');");

            migrationBuilder.Sql(
                "INSERT INTO tenancy.tenant_users (id, tenant_id, user_id, role, created_at) " +
                "SELECT gen_random_uuid(), t.id, u.id, 'Owner', NOW() " +
                "FROM tenancy.tenants t " +
                "CROSS JOIN identity.users u " +
                "WHERE t.slug = 'demo' AND u.email = 'avery.chen@ledgerline.local' " +
                "ON CONFLICT DO NOTHING;");

            migrationBuilder.Sql("UPDATE orders.orders SET tenant_id = (SELECT id FROM tenancy.tenants WHERE slug = 'demo') WHERE tenant_id IS NULL;");
            migrationBuilder.Sql("UPDATE payments.payments SET tenant_id = (SELECT id FROM tenancy.tenants WHERE slug = 'demo') WHERE tenant_id IS NULL;");
            migrationBuilder.Sql("UPDATE payments.refunds SET tenant_id = (SELECT id FROM tenancy.tenants WHERE slug = 'demo') WHERE tenant_id IS NULL;");
            migrationBuilder.Sql("UPDATE webhooks.webhook_events SET tenant_id = (SELECT id FROM tenancy.tenants WHERE slug = 'demo') WHERE tenant_id IS NULL;");
            migrationBuilder.Sql("UPDATE reconciliation.reconciliation_records SET tenant_id = (SELECT id FROM tenancy.tenants WHERE slug = 'demo') WHERE tenant_id IS NULL;");
            migrationBuilder.Sql("UPDATE audit.audit_logs SET tenant_id = (SELECT id FROM tenancy.tenants WHERE slug = 'demo') WHERE tenant_id IS NULL;");

            // Pre-existing QA rows can reference payments that were removed outside the app.
            // Reconciliation records are derived data; keep the payment/order/webhook history but
            // drop records that can never satisfy the tenant-scoped relationship.
            migrationBuilder.Sql(
                "DELETE FROM reconciliation.reconciliation_records r " +
                "WHERE NOT EXISTS (SELECT 1 FROM payments.payments p WHERE p.id = r.payment_id);");

            migrationBuilder.Sql(
                "UPDATE webhooks.webhook_events w SET payment_id = NULL " +
                "WHERE w.payment_id IS NOT NULL " +
                "AND NOT EXISTS (SELECT 1 FROM payments.payments p WHERE p.id = w.payment_id);");

            migrationBuilder.Sql(
                "UPDATE payments.payments p SET order_id = NULL " +
                "WHERE p.order_id IS NOT NULL " +
                "AND NOT EXISTS (SELECT 1 FROM orders.orders o WHERE o.id = p.order_id);");

            migrationBuilder.Sql(
                "DELETE FROM payments.refunds f " +
                "WHERE NOT EXISTS (SELECT 1 FROM payments.payments p WHERE p.id = f.payment_id);");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id", schema: "orders", table: "orders",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id", schema: "payments", table: "payments",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id", schema: "payments", table: "refunds",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id", schema: "webhooks", table: "webhook_events",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id", schema: "reconciliation", table: "reconciliation_records",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id", schema: "audit", table: "audit_logs",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_tenant_id",
                schema: "webhooks",
                table: "webhook_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_tenant_id_payment_id",
                schema: "webhooks",
                table: "webhook_events",
                columns: new[] { "tenant_id", "payment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_tenant_id_provider_provider_event_id",
                schema: "webhooks",
                table: "webhook_events",
                columns: new[] { "tenant_id", "provider", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refunds_tenant_id",
                schema: "payments",
                table: "refunds",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_tenant_id_payment_id",
                schema: "payments",
                table: "refunds",
                columns: new[] { "tenant_id", "payment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_records_payment_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_records_tenant_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_records_tenant_id_order_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_records_payment_success",
                schema: "reconciliation",
                table: "reconciliation_records",
                columns: new[] { "tenant_id", "payment_id" },
                unique: true,
                filter: "\"outcome\" IN ('Matched', 'PartiallyMatched', 'ManuallyMatched')");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id",
                schema: "payments",
                table: "payments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_order_id",
                schema: "payments",
                table: "payments",
                columns: new[] { "tenant_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_provider_provider_payment_id",
                schema: "payments",
                table: "payments",
                columns: new[] { "tenant_id", "provider", "provider_payment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id",
                schema: "orders",
                table: "orders",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_order_number",
                schema: "orders",
                table: "orders",
                columns: new[] { "tenant_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_id",
                schema: "audit",
                table: "audit_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                schema: "identity",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_tenant_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "identity",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_tenant_id",
                schema: "identity",
                table: "refresh_tokens",
                columns: new[] { "user_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                schema: "identity",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_tenant_id_user_id",
                schema: "tenancy",
                table: "tenant_users",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_users_user_id",
                schema: "tenancy",
                table: "tenant_users",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                schema: "tenancy",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_status",
                schema: "tenancy",
                table: "tenants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                schema: "identity",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                schema: "identity",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                schema: "identity",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_users_status",
                schema: "identity",
                table: "users",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_tenant_tenant_id",
                schema: "audit",
                table: "audit_logs",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_tenant_tenant_id",
                schema: "orders",
                table: "orders",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_orders_tenant_id_order_id",
                schema: "payments",
                table: "payments",
                columns: new[] { "tenant_id", "order_id" },
                principalSchema: "orders",
                principalTable: "orders",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_tenant_tenant_id",
                schema: "payments",
                table: "payments",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_reconciliation_records_orders_tenant_id_order_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                columns: new[] { "tenant_id", "order_id" },
                principalSchema: "orders",
                principalTable: "orders",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_reconciliation_records_payments_tenant_id_payment_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                columns: new[] { "tenant_id", "payment_id" },
                principalSchema: "payments",
                principalTable: "payments",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_reconciliation_records_tenant_tenant_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_refunds_payments_tenant_id_payment_id",
                schema: "payments",
                table: "refunds",
                columns: new[] { "tenant_id", "payment_id" },
                principalSchema: "payments",
                principalTable: "payments",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_refunds_tenant_tenant_id",
                schema: "payments",
                table: "refunds",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_webhook_events_payments_tenant_id_payment_id",
                schema: "webhooks",
                table: "webhook_events",
                columns: new[] { "tenant_id", "payment_id" },
                principalSchema: "payments",
                principalTable: "payments",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_webhook_events_tenant_tenant_id",
                schema: "webhooks",
                table: "webhook_events",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_logs_tenant_tenant_id",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_tenant_tenant_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_payments_orders_tenant_id_order_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "fk_payments_tenant_tenant_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "fk_reconciliation_records_orders_tenant_id_order_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropForeignKey(
                name: "fk_reconciliation_records_payments_tenant_id_payment_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropForeignKey(
                name: "fk_reconciliation_records_tenant_tenant_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropForeignKey(
                name: "fk_refunds_payments_tenant_id_payment_id",
                schema: "payments",
                table: "refunds");

            migrationBuilder.DropForeignKey(
                name: "fk_refunds_tenant_tenant_id",
                schema: "payments",
                table: "refunds");

            migrationBuilder.DropForeignKey(
                name: "fk_webhook_events_payments_tenant_id_payment_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropForeignKey(
                name: "fk_webhook_events_tenant_tenant_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_claims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tenant_users",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "user_claims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_logins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_webhook_events_tenant_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropIndex(
                name: "ix_webhook_events_tenant_id_payment_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropIndex(
                name: "ix_webhook_events_tenant_id_provider_provider_event_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropIndex(
                name: "ix_refunds_tenant_id",
                schema: "payments",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_refunds_tenant_id_payment_id",
                schema: "payments",
                table: "refunds");

            migrationBuilder.DropIndex(
                name: "ix_reconciliation_records_payment_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropIndex(
                name: "ix_reconciliation_records_tenant_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropIndex(
                name: "ix_reconciliation_records_tenant_id_order_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropIndex(
                name: "ux_reconciliation_records_payment_success",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_payments_tenant_id_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_tenant_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_tenant_id_order_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_tenant_id_provider_provider_payment_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_orders_tenant_id_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id_order_number",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenant_id",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "payments",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_provider_provider_event_id",
                schema: "webhooks",
                table: "webhook_events",
                columns: new[] { "provider", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_records_payment_success",
                schema: "reconciliation",
                table: "reconciliation_records",
                column: "payment_id",
                unique: true,
                filter: "\"outcome\" IN ('Matched', 'PartiallyMatched', 'ManuallyMatched')");

            migrationBuilder.CreateIndex(
                name: "ix_payments_provider_provider_payment_id",
                schema: "payments",
                table: "payments",
                columns: new[] { "provider", "provider_payment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_order_number",
                schema: "orders",
                table: "orders",
                column: "order_number",
                unique: true);
        }
    }
}
