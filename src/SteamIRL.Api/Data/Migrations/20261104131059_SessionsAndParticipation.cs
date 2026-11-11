using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamIRL.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionsAndParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ResourceLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_idempotency_requests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SkillLevel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EquipmentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                    table.CheckConstraint("ck_session_capacity", "\"Capacity\" BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_session_equipment", "\"EquipmentStatus\" IN ('NotSpecified', 'BallAvailable', 'BallNeeded')");
                    table.CheckConstraint("ck_session_kind", "\"Kind\" IN ('Planned', 'Immediate')");
                    table.CheckConstraint("ck_session_level", "\"SkillLevel\" IN ('Beginner', 'Intermediate', 'Advanced')");
                    table.CheckConstraint("ck_session_time", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_sessions_AspNetUsers_OrganiserId",
                        column: x => x.OrganiserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sessions_location_activities_LocationId_ActivityId",
                        columns: x => new { x.LocationId, x.ActivityId },
                        principalTable: "location_activities",
                        principalColumns: new[] { "LocationId", "ActivityId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participations",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participations", x => new { x.SessionId, x.UserId });
                    table.CheckConstraint("ck_participation_time", "\"LeftAt\" IS NULL OR \"LeftAt\" >= \"JoinedAt\"");
                    table.ForeignKey(
                        name: "FK_participations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_participations_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_requests_ExpiresAt",
                table: "idempotency_requests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_requests_UserId_Key",
                table: "idempotency_requests",
                columns: new[] { "UserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participations_UserId_LeftAt",
                table: "participations",
                columns: new[] { "UserId", "LeftAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_ActivityId_EndsAt",
                table: "sessions",
                columns: new[] { "ActivityId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_LocationId_ActivityId",
                table: "sessions",
                columns: new[] { "LocationId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_LocationId_EndsAt",
                table: "sessions",
                columns: new[] { "LocationId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_OrganiserId_CreatedAt",
                table: "sessions",
                columns: new[] { "OrganiserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_requests");

            migrationBuilder.DropTable(
                name: "participations");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}
