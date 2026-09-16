using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JobsGeParser.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_subscribers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ManageTokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ConfirmationTokenHash = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConfirmationSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmationExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmationSendCount = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnsubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    SuppressedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuppressionReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_subscribers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CategorySlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_events_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Cadence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DigestSendLocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LastDigestSentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MatchFromAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CriteriaHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Query = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TitleContains = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DescriptionContains = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompanyContains = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategorySlugs = table.Column<string[]>(type: "text[]", nullable: true),
                    Cities = table.Column<string[]>(type: "text[]", nullable: true),
                    WorkModes = table.Column<string[]>(type: "text[]", nullable: true),
                    EmploymentTypes = table.Column<string[]>(type: "text[]", nullable: true),
                    Seniorities = table.Column<string[]>(type: "text[]", nullable: true),
                    Languages = table.Column<string[]>(type: "text[]", nullable: true),
                    SalaryMin = table.Column<decimal>(type: "numeric", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "numeric", nullable: true),
                    SalaryCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequireSalary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_subscriptions_alert_subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "alert_subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: true),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_messages_alert_subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "alert_subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_messages_alert_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "alert_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "alert_deliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SkipReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_deliveries_alert_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "alert_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_alert_deliveries_alert_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "alert_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_deliveries_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_JobId",
                table: "alert_deliveries",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_MessageId",
                table: "alert_deliveries",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_SubscriptionId_JobId",
                table: "alert_deliveries",
                columns: new[] { "SubscriptionId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_SubscriptionId_MatchedAt",
                table: "alert_deliveries",
                columns: new[] { "SubscriptionId", "MatchedAt" },
                filter: "\"MessageId\" IS NULL AND \"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_alert_messages_AvailableAt_Id",
                table: "alert_messages",
                columns: new[] { "AvailableAt", "Id" },
                filter: "\"Status\" IN ('Pending', 'Claimed')");

            migrationBuilder.CreateIndex(
                name: "IX_alert_messages_IdempotencyKey",
                table: "alert_messages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_messages_SubscriberId_CreatedAt",
                table: "alert_messages",
                columns: new[] { "SubscriberId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_alert_messages_SubscriptionId",
                table: "alert_messages",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscribers_Channel_Address",
                table: "alert_subscribers",
                columns: new[] { "Channel", "Address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscribers_ConfirmationTokenHash",
                table: "alert_subscribers",
                column: "ConfirmationTokenHash",
                unique: true,
                filter: "\"ConfirmationTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscribers_ManageTokenHash",
                table: "alert_subscribers",
                column: "ManageTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_CategorySlugs",
                table: "alert_subscriptions",
                column: "CategorySlugs")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_Cities",
                table: "alert_subscriptions",
                column: "Cities")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_DigestSendLocalTime",
                table: "alert_subscriptions",
                column: "DigestSendLocalTime",
                filter: "\"Status\" = 'Active' AND \"Cadence\" = 'DailyDigest'");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_EmploymentTypes",
                table: "alert_subscriptions",
                column: "EmploymentTypes")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_Languages",
                table: "alert_subscriptions",
                column: "Languages")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_Seniorities",
                table: "alert_subscriptions",
                column: "Seniorities")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_SubscriberId",
                table: "alert_subscriptions",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_SubscriberId_CriteriaHash",
                table: "alert_subscriptions",
                columns: new[] { "SubscriberId", "CriteriaHash" },
                unique: true,
                filter: "\"Status\" <> 'Deleted'");

            migrationBuilder.CreateIndex(
                name: "IX_alert_subscriptions_WorkModes",
                table: "alert_subscriptions",
                column: "WorkModes")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_job_events_AvailableAt_Id",
                table: "job_events",
                columns: new[] { "AvailableAt", "Id" },
                filter: "\"ProcessedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_job_events_JobId",
                table: "job_events",
                column: "JobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_deliveries");

            migrationBuilder.DropTable(
                name: "job_events");

            migrationBuilder.DropTable(
                name: "alert_messages");

            migrationBuilder.DropTable(
                name: "alert_subscriptions");

            migrationBuilder.DropTable(
                name: "alert_subscribers");
        }
    }
}
