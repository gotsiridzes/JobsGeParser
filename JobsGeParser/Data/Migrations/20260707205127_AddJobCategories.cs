using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobsGeParser.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategorySlug",
                table: "scrape_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ListUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Slug);
                });

            migrationBuilder.CreateTable(
                name: "job_categories",
                columns: table => new
                {
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    CategorySlug = table.Column<string>(type: "character varying(100)", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_categories", x => new { x.JobId, x.CategorySlug });
                    table.ForeignKey(
                        name: "FK_job_categories_categories_CategorySlug",
                        column: x => x.CategorySlug,
                        principalTable: "categories",
                        principalColumn: "Slug",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_categories_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scrape_runs_CategorySlug_StartedAt",
                table: "scrape_runs",
                columns: new[] { "CategorySlug", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_job_categories_CategorySlug",
                table: "job_categories",
                column: "CategorySlug");

            migrationBuilder.AddForeignKey(
                name: "FK_scrape_runs_categories_CategorySlug",
                table: "scrape_runs",
                column: "CategorySlug",
                principalTable: "categories",
                principalColumn: "Slug",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_scrape_runs_categories_CategorySlug",
                table: "scrape_runs");

            migrationBuilder.DropTable(
                name: "job_categories");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropIndex(
                name: "IX_scrape_runs_CategorySlug_StartedAt",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "CategorySlug",
                table: "scrape_runs");
        }
    }
}
