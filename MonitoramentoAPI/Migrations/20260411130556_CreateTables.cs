using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiMonitoramentoAPI.Migrations
{
    /// <inheritdoc />
    public partial class CreateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Timeout",
                table: "ApiMonitors");

            migrationBuilder.AddColumn<string>(
                name: "Plano",
                table: "Users",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedAt",
                table: "ApiMonitors",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusAtual",
                table: "ApiMonitors",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Destino = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MonitorId = table.Column<int>(type: "int", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "int", nullable: false),
                    IsUp = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Erro = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_ApiMonitors_MonitorId",
                        column: x => x.MonitorId,
                        principalTable: "ApiMonitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApiMonitors_UserId",
                table: "ApiMonitors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId",
                table: "Alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_MonitorId",
                table: "Logs",
                column: "MonitorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiMonitors_Users_UserId",
                table: "ApiMonitors",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiMonitors_Users_UserId",
                table: "ApiMonitors");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_ApiMonitors_UserId",
                table: "ApiMonitors");

            migrationBuilder.DropColumn(
                name: "Plano",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastCheckedAt",
                table: "ApiMonitors");

            migrationBuilder.DropColumn(
                name: "StatusAtual",
                table: "ApiMonitors");

            migrationBuilder.AddColumn<int>(
                name: "Timeout",
                table: "ApiMonitors",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
