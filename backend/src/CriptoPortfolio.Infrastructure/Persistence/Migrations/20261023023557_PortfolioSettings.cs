using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriptoPortfolio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioSettings",
                columns: table => new
                {
                    PortfolioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Avatar = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IncludeInTotal = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSettings", x => x.PortfolioId);
                    table.ForeignKey(
                        name: "FK_PortfolioSettings_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioSettings");
        }
    }
}
