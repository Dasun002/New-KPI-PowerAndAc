using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    public partial class ConvertMsanMtcDataYearToInt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.msanmtcdata', N'U') IS NOT NULL
AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.msanmtcdata')
      AND c.name = N'year'
      AND t.name IN (N'varchar', N'nvarchar', N'char', N'nchar')
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM dbo.msanmtcdata
        WHERE [year] IS NOT NULL
          AND LTRIM(RTRIM([year])) <> ''
          AND TRY_CONVERT(int, LTRIM(RTRIM([year]))) IS NULL
    )
    BEGIN
        THROW 50001, 'Cannot convert dbo.msanmtcdata.year to int because one or more values are not numeric.', 1;
    END;

    UPDATE dbo.msanmtcdata
    SET [year] = NULL
    WHERE [year] IS NOT NULL
      AND LTRIM(RTRIM([year])) = '';

    ALTER TABLE dbo.msanmtcdata ALTER COLUMN [year] int NULL;
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.msanmtcdata', N'U') IS NOT NULL
AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.msanmtcdata')
      AND c.name = N'year'
      AND t.name = N'int'
)
BEGIN
    ALTER TABLE dbo.msanmtcdata ALTER COLUMN [year] varchar(10) NULL;
END;
");
        }
    }
}
