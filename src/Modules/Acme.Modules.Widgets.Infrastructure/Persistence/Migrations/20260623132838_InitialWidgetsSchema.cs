using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acme.Modules.Widgets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWidgetsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "widgets");

            migrationBuilder.CreateTable(
                name: "widgets",
                schema: "widgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_widgets", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "widgets",
                schema: "widgets");
        }
    }
}
