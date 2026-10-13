using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SkillFieldsJson = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SportId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TeamPurse = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinSquadSize = table.Column<int>(type: "int", nullable: false),
                    MaxSquadSize = table.Column<int>(type: "int", nullable: false),
                    MinWomenPerSquad = table.Column<int>(type: "int", nullable: false),
                    MaxSpendPerPlayer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReservePurseForUnfilledSlots = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Purse = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InitialPurse = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Teams_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MinBidIncrement = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentPlayerId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auctions_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SkillsJson = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PhotoKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LotOrder = table.Column<int>(type: "int", nullable: false),
                    SoldPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SoldToTeamId = table.Column<int>(type: "int", nullable: true),
                    AuctionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Players_Teams_SoldToTeamId",
                        column: x => x.SoldToTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Players_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false),
                    PlacedByUserId = table.Column<int>(type: "int", nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bids_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bids_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bids_Users_PlacedByUserId",
                        column: x => x.PlacedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_CurrentPlayerId",
                table: "Auctions",
                column: "CurrentPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Status",
                table: "Auctions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_TournamentId",
                table: "Auctions",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionId_ClientRequestId",
                table: "Bids",
                columns: new[] { "AuctionId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionId_PlayerId_Amount",
                table: "Bids",
                columns: new[] { "AuctionId", "PlayerId", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_PlacedByUserId",
                table: "Bids",
                column: "PlacedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_PlayerId",
                table: "Bids",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TeamId",
                table: "Bids",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_AuctionId",
                table: "Players",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SoldToTeamId",
                table: "Players",
                column: "SoldToTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TournamentId_LotOrder",
                table: "Players",
                columns: new[] { "TournamentId", "LotOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_TournamentId_Status",
                table: "Players",
                columns: new[] { "TournamentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Name",
                table: "Sports",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OwnerUserId",
                table: "Teams",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TournamentId_Name",
                table: "Teams",
                columns: new[] { "TournamentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TournamentId_OwnerUserId",
                table: "Teams",
                columns: new[] { "TournamentId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_SportId",
                table: "Tournaments",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Players_CurrentPlayerId",
                table: "Auctions",
                column: "CurrentPlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_Players_CurrentPlayerId",
                table: "Auctions");

            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Auctions");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Sports");
        }
    }
}
