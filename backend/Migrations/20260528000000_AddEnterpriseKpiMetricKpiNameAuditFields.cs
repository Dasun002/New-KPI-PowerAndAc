using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    public partial class AddEnterpriseKpiMetricKpiNameAuditFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add KpiName column (default empty string to satisfy NOT NULL for existing rows)
            migrationBuilder.AddColumn<string>(
                name: "KpiName",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            // Backfill KpiName from the related EnterpriseKpi table
            migrationBuilder.Sql(@"
                UPDATE m
                SET m.KpiName = k.network_engineer_kpi
                FROM dbo.EnterpriseKpiMetrics m
                INNER JOIN dbo.EnterpriseKpi k ON k.Id = m.EnterpriseKpiId
                WHERE m.KpiName = ''
            ");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                type: "datetime2",
                nullable: true);

            // Drop old unique index keyed on EnterpriseKpiId+area_code+month+year
            migrationBuilder.DropIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics");

            // Create new unique index keyed on area_code+KpiName+month+year
            migrationBuilder.CreateIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                columns: new[] { "area_code", "KpiName", "month", "year" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics");

            migrationBuilder.CreateIndex(
                name: "UQ_EnterpriseKpiMetrics_Row",
                schema: "dbo",
                table: "EnterpriseKpiMetrics",
                columns: new[] { "EnterpriseKpiId", "area_code", "month", "year" },
                unique: true);

            migrationBuilder.DropColumn(name: "KpiName", schema: "dbo", table: "EnterpriseKpiMetrics");
            migrationBuilder.DropColumn(name: "CreatedAt", schema: "dbo", table: "EnterpriseKpiMetrics");
            migrationBuilder.DropColumn(name: "UpdatedAt", schema: "dbo", table: "EnterpriseKpiMetrics");
        }
    }
}
