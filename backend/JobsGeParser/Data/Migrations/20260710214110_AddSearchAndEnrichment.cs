using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace JobsGeParser.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchAndEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionHtml",
                table: "jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "jobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EnrichedAt",
                table: "jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnrichmentVersion",
                table: "jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LanguageRequirement",
                table: "jobs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryCurrency",
                table: "jobs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMax",
                table: "jobs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMin",
                table: "jobs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryPeriod",
                table: "jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Seniority",
                table: "jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkMode",
                table: "jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "jobs",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "setweight(to_tsvector('simple', coalesce(\"Name\", '')), 'A') ||\r\nsetweight(to_tsvector('simple', coalesce(\"Company\", '')), 'B') ||\r\nsetweight(to_tsvector('simple', coalesce(\"Description\", '')), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_jobs_Company",
                table: "jobs",
                column: "Company")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_Name",
                table: "jobs",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_SearchVector",
                table: "jobs",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_jobs_Company",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "IX_jobs_Name",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "IX_jobs_SearchVector",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "City",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "DescriptionHtml",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "EnrichedAt",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "EnrichmentVersion",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "LanguageRequirement",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryCurrency",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryPeriod",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "Seniority",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "WorkMode",
                table: "jobs");
        }
    }
}
