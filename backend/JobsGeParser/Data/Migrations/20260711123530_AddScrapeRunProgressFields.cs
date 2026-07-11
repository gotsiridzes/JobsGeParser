using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobsGeParser.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapeRunProgressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JobsDiscovered",
                table: "scrape_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JobsNeedingDetails",
                table: "scrape_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ListingPagesFetched",
                table: "scrape_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "scrape_runs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProgressUpdatedAt",
                table: "scrape_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE scrape_runs
                SET "Phase" = CASE "Status"
                    WHEN 'Completed' THEN 'Completed'
                    WHEN 'Failed' THEN 'Failed'
                    ELSE 'Discovering'
                END
                WHERE "Phase" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobsDiscovered",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "JobsNeedingDetails",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "ListingPagesFetched",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "ProgressUpdatedAt",
                table: "scrape_runs");
        }
    }
}
