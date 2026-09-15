using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaceCore.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingsAndServiceFreezes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalHallId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Removed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PricePerHour = table.Column<decimal>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceFreezes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Removed = table.Column<bool>(type: "INTEGER", nullable: false),
                    FreezeDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BookingEntityId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFreezes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceFreezes_Bookings_BookingEntityId",
                        column: x => x.BookingEntityId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFreezes_BookingEntityId",
                table: "ServiceFreezes",
                column: "BookingEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceFreezes");

            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
