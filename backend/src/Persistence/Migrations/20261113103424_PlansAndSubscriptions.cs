using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlansAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "features",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tagline = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    audience = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    monthly_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_note = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_recommended = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_features",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_included = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_features", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_features_features_feature_id",
                        column: x => x.feature_id,
                        principalSchema: "tenancy",
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_features_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "tenancy",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_limits",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<int>(type: "integer", nullable: true),
                    display = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_limits", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_limits_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "tenancy",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    activation_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payment_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    pending_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pending_plan_effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscriptions_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "tenancy",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_history",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscription_history_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalSchema: "tenancy",
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_subscription_history_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_features_key",
                schema: "tenancy",
                table: "features",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_features_feature_id",
                schema: "tenancy",
                table: "plan_features",
                column: "feature_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_features_plan_id_feature_id",
                schema: "tenancy",
                table: "plan_features",
                columns: new[] { "plan_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_limits_plan_id_key",
                schema: "tenancy",
                table: "plan_limits",
                columns: new[] { "plan_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_code",
                schema: "tenancy",
                table: "plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_sort_order",
                schema: "tenancy",
                table: "plans",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_subscription_id",
                schema: "tenancy",
                table: "subscription_history",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_tenant_id",
                schema: "tenancy",
                table: "subscription_history",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_tenant_id_idempotency_key",
                schema: "tenancy",
                table: "subscription_history",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_history_tenant_id_occurred_at",
                schema: "tenancy",
                table: "subscription_history",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_plan_id",
                schema: "tenancy",
                table: "subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_status_current_period_end",
                schema: "tenancy",
                table: "subscriptions",
                columns: new[] { "status", "current_period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id",
                schema: "tenancy",
                table: "subscriptions",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_features",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "plan_limits",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "subscription_history",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "features",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "tenancy");
        }
    }
}
