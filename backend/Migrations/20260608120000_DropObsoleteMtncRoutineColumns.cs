using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class DropObsoleteMtncRoutineColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop obsolete columns from dbo.mtncroutinetable1
            migrationBuilder.DropColumn(
                name: "platform",
                schema: "dbo",
                table: "mtncroutinetable1");

            migrationBuilder.DropColumn(
                name: "responsibleDGM",
                schema: "dbo",
                table: "mtncroutinetable1");

            migrationBuilder.DropColumn(
                name: "definedOLADetails",
                schema: "dbo",
                table: "mtncroutinetable1");

            migrationBuilder.DropColumn(
                name: "dataSources",
                schema: "dbo",
                table: "mtncroutinetable1");

            migrationBuilder.DropColumn(
                name: "v",
                schema: "dbo",
                table: "mtncroutinetable1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore dropped columns (nullable to avoid data-loss issues on rollback)
            migrationBuilder.AddColumn<string>(
                name: "platform",
                schema: "dbo",
                table: "mtncroutinetable1",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "responsibleDGM",
                schema: "dbo",
                table: "mtncroutinetable1",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "definedOLADetails",
                schema: "dbo",
                table: "mtncroutinetable1",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "dataSources",
                schema: "dbo",
                table: "mtncroutinetable1",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte>(
                name: "v",
                schema: "dbo",
                table: "mtncroutinetable1",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }
    }
}
