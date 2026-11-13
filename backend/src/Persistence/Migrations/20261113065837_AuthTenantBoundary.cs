using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthTenantBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_refresh_tokens_tenants_tenant_id",
                schema: "identity",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_tenant_id",
                schema: "identity",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id_tenant_id",
                schema: "identity",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "identity",
                table: "refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_revoked_at",
                schema: "identity",
                table: "refresh_tokens",
                columns: new[] { "user_id", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id_revoked_at",
                schema: "identity",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "identity",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_tenant_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_tenant_id",
                schema: "identity",
                table: "refresh_tokens",
                columns: new[] { "user_id", "tenant_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_tokens_tenants_tenant_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "tenant_id",
                principalSchema: "tenancy",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
