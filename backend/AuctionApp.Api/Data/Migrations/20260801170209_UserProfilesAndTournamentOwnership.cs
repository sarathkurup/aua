using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserProfilesAndTournamentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "Tournaments",
                type: "int",
                nullable: true);

            // Every existing account shares the all-zero default the AddColumn above wrote,
            // which the unique index would reject. NEWID() runs per row, giving each
            // account its own external id before the index is created.
            migrationBuilder.Sql("UPDATE [Users] SET [PublicId] = NEWID();");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicId",
                table: "Users",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_OwnerUserId",
                table: "Tournaments",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tournaments_Users_OwnerUserId",
                table: "Tournaments",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tournaments_Users_OwnerUserId",
                table: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_Users_PublicId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tournaments_OwnerUserId",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Tournaments");
        }
    }
}
