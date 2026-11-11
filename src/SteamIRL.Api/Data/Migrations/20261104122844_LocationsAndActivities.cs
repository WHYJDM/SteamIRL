using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SteamIRL.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocationsAndActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CoordinateNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AccessNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                    table.CheckConstraint("ck_location_latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_location_longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_location_name", "length(trim(\"Name\")) > 0");
                    table.CheckConstraint("ck_location_status", "\"ApprovalStatus\" IN ('Pending', 'Approved', 'Rejected', 'Removed')");
                });

            migrationBuilder.CreateTable(
                name: "location_activities",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_activities", x => new { x.LocationId, x.ActivityId });
                    table.ForeignKey(
                        name: "FK_location_activities_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_location_activities_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "locations",
                columns: new[] { "Id", "AccessNotes", "Address", "ApprovalStatus", "CoordinateNote", "Description", "Latitude", "Longitude", "Name", "SourceCheckedAt", "SourceUrl", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("aaaa1111-1111-4111-8111-111111111111"), "Оператор публикует правила очереди и ограничения для тренировок. Перед поездкой проверь действующие условия по ссылке на источник.", "Upės g. 6, Vilnius", "Approved", "Ориентировочная точка комплекса, не отдельной площадки или входа. Уточни место встречи по адресу.", "Открытый спортивный комплекс у реки Нерис с площадками для пляжного волейбола и баскетбола.", 54.6948m, 25.2764m, "Площадки у Белого моста", new DateTimeOffset(new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://baltasistiltas.lt/bendra-informacija/", new DateTimeOffset(new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("aaaa2222-2222-4222-8222-222222222222"), "Сведения об активностях взяты из презентации оператора. Текущий доступ, состояние оборудования и часы работы требуют уточнения.", "Kaukysos g., Vilnius", "Approved", "Ориентировочная точка района комплекса. Точный вход и отдельные площадки требуют проверки на месте.", "Общественный спортивный комплекс в районе Паплауя. В материалах Active Vilnius указаны футбол, баскетбол, пляжный волейбол и настольный теннис.", 54.6764m, 25.3161m, "Спортивный комплекс Kaukysos", new DateTimeOffset(new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "https://activevilnius.lt/AV-pristatymas.pdf", new DateTimeOffset(new DateTime(2026, 11, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "location_activities",
                columns: new[] { "ActivityId", "LocationId" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-4111-8111-111111111111"), new Guid("aaaa1111-1111-4111-8111-111111111111") },
                    { new Guid("22222222-2222-4222-8222-222222222222"), new Guid("aaaa1111-1111-4111-8111-111111111111") },
                    { new Guid("11111111-1111-4111-8111-111111111111"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                    { new Guid("22222222-2222-4222-8222-222222222222"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                    { new Guid("33333333-3333-4333-8333-333333333333"), new Guid("aaaa2222-2222-4222-8222-222222222222") },
                    { new Guid("44444444-4444-4444-8444-444444444444"), new Guid("aaaa2222-2222-4222-8222-222222222222") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_location_activities_ActivityId_LocationId",
                table: "location_activities",
                columns: new[] { "ActivityId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_locations_ApprovalStatus_Name_Id",
                table: "locations",
                columns: new[] { "ApprovalStatus", "Name", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location_activities");

            migrationBuilder.DropTable(
                name: "locations");
        }
    }
}
