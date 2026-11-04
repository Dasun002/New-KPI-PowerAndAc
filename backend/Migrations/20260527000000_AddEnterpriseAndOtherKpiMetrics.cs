using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    public partial class AddEnterpriseAndOtherKpiMetrics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnterpriseKpiMetrics",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnterpriseKpiId = table.Column<int>(type: "int", nullable: false),
                    area_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    kpi_value = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    month = table.Column<byte>(type: "tinyint", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnterpriseKpiMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnterpriseKpiMetrics_EnterpriseKpi",
                        column: x => x.EnterpriseKpiId,
                        principalSchema: "dbo",
                        principalTable: "EnterpriseKpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OtherKpiMetrics",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtherKpiId = table.Column<int>(type: "int", nullable: false),
                    area_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    kpi_value = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    month = table.Column<byte>(type: "tinyint", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherKpiMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherKpiMetrics_OtherKpi",
                        column: x => x.OtherKpiId,
                        principalSchema: "dbo",
                        principalTable: "OtherOperatorKpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                columns: new[] { "EnterpriseKpiId", "area_code", "month", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_OtherKpiMetrics_Row",
                schema: "dbo",
                table: "OtherKpiMetrics",
                columns: new[] { "OtherKpiId", "area_code", "month", "year" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnterpriseKpiMetrics",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OtherKpiMetrics",
                schema: "dbo");
        }
    }
}