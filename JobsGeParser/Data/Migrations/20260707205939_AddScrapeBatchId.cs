using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobsGeParser.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScrapeBatchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "scrape_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_scrape_runs_BatchId",
                table: "scrape_runs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_scrape_runs_Status",
                table: "scrape_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_scrape_runs_BatchId",
                table: "scrape_runs");

            migrationBuilder.DropIndex(
                name: "IX_scrape_runs_Status",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "scrape_runs");
        }
    }
}
