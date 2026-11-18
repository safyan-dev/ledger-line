using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCustomerReferenceUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_customer_reference",
                schema: "orders",
                table: "orders");

            // Clean duplicate references
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY tenant_id, customer_reference
                               ORDER BY created_at, id
                           ) AS position
                    FROM orders.orders
                    WHERE customer_reference IS NOT NULL
                )
                UPDATE orders.orders AS orders
                SET customer_reference = NULL
                FROM ranked
                WHERE orders.id = ranked.id
                  AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_customer_reference",
                schema: "orders",
                table: "orders",
                columns: new[] { "tenant_id", "customer_reference" },
                unique: true,
                filter: "customer_reference IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id_customer_reference",
                schema: "orders",
                table: "orders");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_reference",
                schema: "orders",
                table: "orders",
                column: "customer_reference");
        }
    }
}
