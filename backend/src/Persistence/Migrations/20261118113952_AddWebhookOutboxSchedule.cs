using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookOutboxSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                schema: "webhooks",
                table: "webhook_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_status_next_attempt_at",
                schema: "webhooks",
                table: "webhook_events",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_webhook_events_status_next_attempt_at",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "webhooks",
                table: "webhook_events");
        }
    }
}
