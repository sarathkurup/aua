using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetainedMembersAndAccountRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamRole",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AuctionName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Contact = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRequests_Users_CreatedUserId",
                        column: x => x.CreatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountRequests_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_CreatedUserId",
                table: "AccountRequests",
                column: "CreatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_DecidedByUserId",
                table: "AccountRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_Status",
                table: "AccountRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRequests_Username",
                table: "AccountRequests",
                column: "Username",
                unique: true,
                filter: "[Status] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRequests");

            migrationBuilder.DropColumn(
                name: "TeamRole",
                table: "Players");
        }
    }
}
