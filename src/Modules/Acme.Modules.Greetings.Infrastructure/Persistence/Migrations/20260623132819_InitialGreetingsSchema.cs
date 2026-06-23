using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acme.Modules.Greetings.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGreetingsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "greetings");

            migrationBuilder.CreateTable(
                name: "greetings",
                schema: "greetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_greetings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "greetings",
                schema: "greetings");
        }
    }
}
