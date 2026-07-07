using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobsGeParser.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoPhaseScrapeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DetailsFetched",
                table: "scrape_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DetailsSkipped",
                table: "scrape_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DetailsFetchedAt",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE jobs
                SET "DetailsFetchedAt" = COALESCE("UpdatedAt", "LastSeenAt")
                WHERE "Description" IS NOT NULL AND "DetailsFetchedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailsFetched",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "DetailsSkipped",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "DetailsFetchedAt",
                table: "jobs");
        }
    }
}
