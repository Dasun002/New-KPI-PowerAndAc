using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteOtherOperatorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OtherKpiMetrics_OtherKpi",
                schema: "dbo",
                table: "OtherKpiMetrics");

            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_EnterpriseKpiMetrics_Row' AND object_id = OBJECT_ID('dbo.EnterpriseKpiMetrics')) DROP INDEX UQ_EnterpriseKpiMetrics_Row ON dbo.EnterpriseKpiMetrics;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_finaldatatables",
                schema: "dbo",
                table: "finaldatatables");

            migrationBuilder.DropColumn(
                name: "Password",
                schema: "dbo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KpiName",
                schema: "dbo",
                table: "EnterpriseKpiMetrics");

            migrationBuilder.DropColumn(
                name: "month",
                schema: "dbo",
                table: "finaldatatables");

            migrationBuilder.DropColumn(
                name: "year",
                schema: "dbo",
                table: "finaldatatables");

            migrationBuilder.RenameTable(
                name: "finaldatatables",
                schema: "dbo",
                newName: "KpiDefinition",
                newSchema: "dbo");

            migrationBuilder.RenameColumn(
                name: "year",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "month",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "Month");

            migrationBuilder.RenameColumn(
                name: "kpi_value",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "KpiValue");

            migrationBuilder.RenameColumn(
                name: "area_code",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "AreaCode");

            migrationBuilder.RenameColumn(
                name: "year",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "month",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "Month");

            migrationBuilder.RenameColumn(
                name: "kpi_value",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "KpiValue");

            migrationBuilder.RenameColumn(
                name: "area_code",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "Site");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "OtherKpiMetrics",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "updatedAt",
                schema: "dbo",
                table: "KpiDefinition",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "createdAt",
                schema: "dbo",
                table: "KpiDefinition",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "dbo",
                table: "KpiDefinition",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "totalPoints",
                schema: "dbo",
                table: "KpiDefinition",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_KpiDefinition",
                schema: "dbo",
                table: "KpiDefinition",
                column: "id");

            migrationBuilder.CreateTable(
                name: "AgedNetworkFailureMetrics",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    area_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    platform_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    has_unavailability = table.Column<int>(type: "int", nullable: false),
                    month = table.Column<byte>(type: "tinyint", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgedNetworkFailureMetrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ipnwmtcdata",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    designation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    year = table.Column<int>(type: "int", nullable: true),
                    month = table.Column<string>(type: "varchar(10)", nullable: true),
                    scheduled = table.Column<int>(type: "int", nullable: true),
                    attended = table.Column<int>(type: "int", nullable: true),
                    cumulative_count = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ipnwmtcdata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "msanmtcdata",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    designation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    year = table.Column<int>(type: "int", nullable: true),
                    month = table.Column<string>(type: "varchar(10)", nullable: true),
                    scheduled = table.Column<int>(type: "int", nullable: true),
                    attended = table.Column<int>(type: "int", nullable: true),
                    Cumulative_Sched = table.Column<int>(type: "int", nullable: false),
                    Cumulative_Achieved = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_msanmtcdata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OtherKpi",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    network_engineer_kpi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    division = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    section = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    kpi_percent = table.Column<decimal>(type: "decimal(6,3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherKpi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtherOperatorKpiMetrics",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OtherOperatorKpiId = table.Column<int>(type: "int", nullable: false),
                    Site = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    kpi_value = table.Column<decimal>(type: "decimal(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherOperatorKpiMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherOperatorKpiMetrics_OtherOperatorKpi",
                        column: x => x.OtherOperatorKpiId,
                        principalSchema: "dbo",
                        principalTable: "OtherOperatorKpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "slbnmtcdata",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    designation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    year = table.Column<int>(type: "int", nullable: true),
                    month = table.Column<string>(type: "varchar(10)", nullable: true),
                    scheduled = table.Column<int>(type: "int", nullable: true),
                    attended = table.Column<int>(type: "int", nullable: true),
                    Cumulative_Sched = table.Column<int>(type: "int", nullable: false),
                    Cumulative_Achieved = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slbnmtcdata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "towermtcdata",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    designation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    year = table.Column<short>(type: "smallint", nullable: true),
                    month = table.Column<string>(type: "nchar(10)", nullable: true),
                    scheduled = table.Column<int>(type: "int", nullable: true),
                    attended = table.Column<int>(type: "int", nullable: true),
                    cumulative_count = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_towermtcdata", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                columns: new[] { "EnterpriseKpiId", "Site", "Month", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AgedNetworkFailureMetrics_Row",
                schema: "dbo",
                table: "AgedNetworkFailureMetrics",
                columns: new[] { "area_code", "platform_type", "month", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherOperatorKpiMetrics_OtherOperatorKpiId",
                schema: "dbo",
                table: "OtherOperatorKpiMetrics",
                column: "OtherOperatorKpiId");

            migrationBuilder.AddForeignKey(
                name: "FK_OtherKpiMetrics_OtherKpi",
                schema: "dbo",
                table: "OtherKpiMetrics",
                column: "OtherKpiId",
                principalSchema: "dbo",
                principalTable: "OtherKpi",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OtherKpiMetrics_OtherKpi",
                schema: "dbo",
                table: "OtherKpiMetrics");

            migrationBuilder.DropTable(
                name: "AgedNetworkFailureMetrics",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ipnwmtcdata",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "msanmtcdata",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OtherKpi",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OtherOperatorKpiMetrics",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "slbnmtcdata",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "towermtcdata",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics");

            migrationBuilder.DropPrimaryKey(
                name: "PK_KpiDefinition",
                schema: "dbo",
                table: "KpiDefinition");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "OtherKpiMetrics");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "dbo",
                table: "KpiDefinition");

            migrationBuilder.DropColumn(
                name: "totalPoints",
                schema: "dbo",
                table: "KpiDefinition");

            migrationBuilder.RenameTable(
                name: "KpiDefinition",
                schema: "dbo",
                newName: "finaldatatables",
                newSchema: "dbo");

            migrationBuilder.RenameColumn(
                name: "Year",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "year");

            migrationBuilder.RenameColumn(
                name: "Month",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "month");

            migrationBuilder.RenameColumn(
                name: "KpiValue",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "kpi_value");

            migrationBuilder.RenameColumn(
                name: "AreaCode",
                schema: "dbo",
                table: "OtherKpiMetrics",
                newName: "area_code");

            migrationBuilder.RenameColumn(
                name: "Year",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "year");

            migrationBuilder.RenameColumn(
                name: "Month",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "month");

            migrationBuilder.RenameColumn(
                name: "KpiValue",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "kpi_value");

            migrationBuilder.RenameColumn(
                name: "Site",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                newName: "area_code");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                schema: "dbo",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KpiName",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "updatedAt",
                schema: "dbo",
                table: "finaldatatables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "createdAt",
                schema: "dbo",
                table: "finaldatatables",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "month",
                schema: "dbo",
                table: "finaldatatables",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<short>(
                name: "year",
                schema: "dbo",
                table: "finaldatatables",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_finaldatatables",
                schema: "dbo",
                table: "finaldatatables",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                columns: new[] { "area_code", "KpiName", "month", "year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OtherKpiMetrics_OtherKpi",
                schema: "dbo",
                table: "OtherKpiMetrics",
                column: "OtherKpiId",
                principalSchema: "dbo",
                principalTable: "OtherOperatorKpi",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
