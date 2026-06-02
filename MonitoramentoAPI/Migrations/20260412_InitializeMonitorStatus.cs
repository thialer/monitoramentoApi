using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiMonitoramentoAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitializeMonitorStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update all monitors with empty or null StatusAtual to "UP" (default state)
            migrationBuilder.Sql(
                "UPDATE `ApiMonitors` SET `StatusAtual` = 'UP' WHERE `StatusAtual` IS NULL OR `StatusAtual` = '';"
            );

            // Change StatusAtual to allow null values for flexibility
            migrationBuilder.AlterColumn<string>(
                name: "StatusAtual",
                table: "ApiMonitors",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: false)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StatusAtual",
                table: "ApiMonitors",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
