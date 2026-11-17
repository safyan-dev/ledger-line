using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconciliationSuccessUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reconciliation_records_payment_id",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_records_payment_success",
                schema: "reconciliation",
                table: "reconciliation_records",
                column: "payment_id",
                unique: true,
                filter: "\"outcome\" IN ('Matched', 'PartiallyMatched', 'ManuallyMatched')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_reconciliation_records_payment_success",
                schema: "reconciliation",
                table: "reconciliation_records");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_records_payment_id",
                schema: "reconciliation",
                table: "reconciliation_records",
                column: "payment_id");
        }
    }
}
