using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BidRetraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RetractedAt",
                table: "Bids",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetractedByUserId",
                table: "Bids",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_RetractedByUserId",
                table: "Bids",
                column: "RetractedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Users_RetractedByUserId",
                table: "Bids",
                column: "RetractedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Users_RetractedByUserId",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Bids_RetractedByUserId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "RetractedAt",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "RetractedByUserId",
                table: "Bids");
        }
    }
}
