using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                schema: "identity",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                "UPDATE identity.refresh_tokens SET family_id = gen_random_uuid() WHERE family_id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_family_id_revoked_at",
                schema: "identity",
                table: "refresh_tokens",
                columns: new[] { "user_id", "family_id", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id_family_id_revoked_at",
                schema: "identity",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "family_id",
                schema: "identity",
                table: "refresh_tokens");
        }
    }
}
