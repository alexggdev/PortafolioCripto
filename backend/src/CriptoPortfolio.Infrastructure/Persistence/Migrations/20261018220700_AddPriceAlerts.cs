using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriptoPortfolio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    TargetPriceUsd = table.Column<decimal>(type: "decimal(28,18)", precision: 28, scale: 18, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TriggeredPriceUsd = table.Column<decimal>(type: "decimal(28,18)", precision: 28, scale: 18, nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceAlerts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceAlerts_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceAlerts_AssetId",
                table: "PriceAlerts",
                column: "AssetId",
                filter: "[TriggeredAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PriceAlerts_UserId_CreatedAtUtc",
                table: "PriceAlerts",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceAlerts");
        }
    }
}
