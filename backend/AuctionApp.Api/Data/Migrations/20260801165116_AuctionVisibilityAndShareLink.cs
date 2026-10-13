using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuctionVisibilityAndShareLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auctions_Status",
                table: "Auctions");

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Auctions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "Auctions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // AddColumn stamps every existing row with the same all-zero default, which the
            // unique index below would immediately reject. NEWID() is evaluated per row, so
            // this gives each pre-existing auction its own share link before the index lands.
            migrationBuilder.Sql("UPDATE [Auctions] SET [PublicId] = NEWID();");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_PublicId",
                table: "Auctions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Status_Visibility",
                table: "Auctions",
                columns: new[] { "Status", "Visibility" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auctions_PublicId",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_Status_Visibility",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Auctions");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Status",
                table: "Auctions",
                column: "Status");
        }
    }
}
