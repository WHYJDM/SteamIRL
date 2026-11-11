using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SteamIRL.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.id);
                    table.CheckConstraint("ck_activities_slug", "slug ~ '^[a-z]+(-[a-z]+)*$'");
                });

            migrationBuilder.InsertData(
                table: "activities",
                columns: new[] { "id", "description", "name", "slug" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-4111-8111-111111111111"), "Найди площадку и компанию для игры в волейбол.", "Волейбол", "volleyball" },
                    { new Guid("22222222-2222-4222-8222-222222222222"), "Собери команду для игры на открытой площадке.", "Баскетбол", "basketball" },
                    { new Guid("33333333-3333-4333-8333-333333333333"), "Планируй игру и находи недостающих игроков.", "Футбол", "football" },
                    { new Guid("44444444-4444-4444-8444-444444444444"), "Найди место для партии и партнёра по игре.", "Настольный теннис", "table-tennis" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_slug",
                table: "activities",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activities");
        }
    }
}
