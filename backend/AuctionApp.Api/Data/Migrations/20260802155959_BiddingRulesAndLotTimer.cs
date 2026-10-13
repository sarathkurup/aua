using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BiddingRulesAndLotTimer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoTimeoutSeconds",
                table: "Auctions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IncrementRulesJson",
                table: "Auctions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LotDeadlineAt",
                table: "Auctions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumBid",
                table: "Auctions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoTimeoutSeconds",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "IncrementRulesJson",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "LotDeadlineAt",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "MinimumBid",
                table: "Auctions");
        }
    }
}
