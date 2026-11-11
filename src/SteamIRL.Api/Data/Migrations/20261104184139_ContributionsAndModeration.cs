using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamIRL.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContributionsAndModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "locations",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "location_activities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "content_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reports", x => x.Id);
                    table.CheckConstraint("ck_report_reason", "\"Reason\" IN ('Inaccurate','Spam','Abuse','AccessIssue','Safety','Other')");
                    table.CheckConstraint("ck_report_state", "\"State\" IN ('Open','Resolved','Dismissed')");
                    table.CheckConstraint("ck_report_target", "(\"LocationId\" IS NOT NULL) <> (\"SessionId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_content_reports_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_content_reports_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_content_reports_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BaseRevision = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CoordinateNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AccessNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OriginalDataJson = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_submissions", x => x.Id);
                    table.CheckConstraint("ck_submission_coordinates", "\"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_submission_kind", "\"Kind\" IN ('New','Correction')");
                    table.CheckConstraint("ck_submission_revision", "\"BaseRevision\" >= 1");
                    table.CheckConstraint("ck_submission_state", "\"State\" IN ('Pending','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_location_submissions_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_location_submissions_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: false),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: false),
                    AffectedSessions = table.Column<int>(type: "integer", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_decisions", x => x.Id);
                    table.CheckConstraint("ck_decision_action", "(\"SubmissionId\" IS NOT NULL AND \"Action\" IN ('Approve','Reject')) OR (\"ReportId\" IS NOT NULL AND \"Action\" IN ('Resolve','Dismiss','RemoveContent'))");
                    table.CheckConstraint("ck_decision_reason", "length(trim(\"Reason\")) >= 10");
                    table.CheckConstraint("ck_decision_target", "(\"SubmissionId\" IS NOT NULL) <> (\"ReportId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_moderation_decisions_AspNetUsers_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_decisions_content_reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "content_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_decisions_location_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "location_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_activities",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_activities", x => new { x.SubmissionId, x.ActivityId });
                    table.ForeignKey(
                        name: "FK_submission_activities_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_submission_activities_location_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "location_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { new Guid("dddd1111-1111-4111-8111-111111111111"), "moderator-role-v1", "Moderator", "MODERATOR" });

            migrationBuilder.UpdateData(
                table: "location_activities",
                keyColumns: new[] { "ActivityId", "LocationId" },
                keyValues: new object[] { new Guid("11111111-1111-4111-8111-111111111111"), new Guid("aaaa1111-1111-4111-8111-111111111111") },
                column: "IsEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "location_activities",
                keyColumns: new[] { "ActivityId", "LocationId" },
                keyValues: new object[] { new Guid("22222222-2222-4222-8222-222222222222"), new Guid("aaaa1111-1111-4111-8111-111111111111") },
                column: "IsEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "location_activities",
                keyColumns: new[] { "ActivityId", "LocationId" },
                keyValues: new object[] { new Guid("11111111-1111-4111-8111-111111111111"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                column: "IsEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "location_activities",
                keyColumns: new[] { "ActivityId", "LocationId" },
                keyValues: new object[] { new Guid("22222222-2222-4222-8222-222222222222"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                column: "IsEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "location_activities",
                keyColumns: new[] { "ActivityId", "LocationId" },
                keyValues: new object[] { new Guid("33333333-3333-4333-8333-333333333333"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                column: "IsEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "location_activities",
                keyColumns: new[] { "ActivityId", "LocationId" },
                keyValues: new object[] { new Guid("44444444-4444-4444-8444-444444444444"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                column: "IsEnabled",
                value: true);

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "Id",
                keyValue: new Guid("aaaa1111-1111-4111-8111-111111111111"),
                column: "Revision",
                value: 1L);

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "Id",
                keyValue: new Guid("aaaa2222-2222-4222-8222-222222222222"),
                column: "Revision",
                value: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_location_revision",
                table: "locations",
                sql: "\"Revision\" >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_AuthorId_LocationId_Reason",
                table: "content_reports",
                columns: new[] { "AuthorId", "LocationId", "Reason" },
                unique: true,
                filter: "\"State\" = 'Open' AND \"LocationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_AuthorId_SessionId_Reason",
                table: "content_reports",
                columns: new[] { "AuthorId", "SessionId", "Reason" },
                unique: true,
                filter: "\"State\" = 'Open' AND \"SessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_AuthorId_SubmittedAt",
                table: "content_reports",
                columns: new[] { "AuthorId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_LocationId",
                table: "content_reports",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_SessionId",
                table: "content_reports",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_State_SubmittedAt",
                table: "content_reports",
                columns: new[] { "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_location_submissions_AuthorId_Fingerprint",
                table: "location_submissions",
                columns: new[] { "AuthorId", "Fingerprint" },
                unique: true,
                filter: "\"State\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_location_submissions_AuthorId_SubmittedAt",
                table: "location_submissions",
                columns: new[] { "AuthorId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_location_submissions_Fingerprint",
                table: "location_submissions",
                column: "Fingerprint",
                unique: true,
                filter: "\"Kind\" = 'New' AND \"State\" = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_location_submissions_LocationId",
                table: "location_submissions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_location_submissions_State_SubmittedAt",
                table: "location_submissions",
                columns: new[] { "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_decisions_ModeratorId",
                table: "moderation_decisions",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_decisions_ReportId",
                table: "moderation_decisions",
                column: "ReportId",
                unique: true,
                filter: "\"ReportId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_decisions_SubmissionId",
                table: "moderation_decisions",
                column: "SubmissionId",
                unique: true,
                filter: "\"SubmissionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_submission_activities_ActivityId",
                table: "submission_activities",
                column: "ActivityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderation_decisions");

            migrationBuilder.DropTable(
                name: "submission_activities");

            migrationBuilder.DropTable(
                name: "content_reports");

            migrationBuilder.DropTable(
                name: "location_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_location_revision",
                table: "locations");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("dddd1111-1111-4111-8111-111111111111"));

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "location_activities");
        }
    }
}
