using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class JerseyNumberAndSoldAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JerseyNumber",
                table: "Players",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoldAt",
                table: "Players",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JerseyNumber",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SoldAt",
                table: "Players");
        }
    }
}
