-- 005: Typed Achievement % for Power & A/C components scored with DirectPercent.
-- Creates dbo.PowerAcAchievement only. Safe to re-run. Does not change existing tables or rows.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PowerAcAchievement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PowerAcAchievement (
        Id                 int           IDENTITY(1,1) NOT NULL,
        EngineerGroupId    int           NOT NULL,
        [Year]             smallint      NOT NULL,
        [Month]            tinyint       NOT NULL,
        ComponentId        int           NOT NULL,
        AchievementPercent decimal(9,4)  NOT NULL,
        [Status]           nvarchar(20)  NOT NULL CONSTRAINT DF_PowerAcAchievement_Status DEFAULT N'Entered',
        EnteredBy          nvarchar(100) NULL,
        EnteredAt          datetime2     NOT NULL CONSTRAINT DF_PowerAcAchievement_EnteredAt DEFAULT SYSUTCDATETIME(),
        VerifiedBy         nvarchar(100) NULL,
        VerifiedAt         datetime2     NULL,
        UpdatedAt          datetime2     NULL,
        UpdatedBy          nvarchar(100) NULL,
        CONSTRAINT PK_PowerAcAchievement PRIMARY KEY (Id),
        CONSTRAINT FK_PowerAcAchievement_Group FOREIGN KEY (EngineerGroupId) REFERENCES dbo.PowerAcEngineerGroup (Id),
        CONSTRAINT FK_PowerAcAchievement_Component FOREIGN KEY (ComponentId) REFERENCES dbo.PowerAcComponent (Id),
        CONSTRAINT CK_PowerAcAchievement_Month CHECK ([Month] BETWEEN 1 AND 12),
        CONSTRAINT CK_PowerAcAchievement_Percent CHECK (AchievementPercent BETWEEN 0 AND 100),
        CONSTRAINT CK_PowerAcAchievement_Status CHECK ([Status] IN (N'Entered', N'Verified'))
    );
    CREATE UNIQUE INDEX UQ_PowerAcAchievement_Group_Period_Component
        ON dbo.PowerAcAchievement (EngineerGroupId, [Year], [Month], ComponentId);
END;

COMMIT TRANSACTION;
SELECT COUNT(*) AS PowerAcAchievement_rows FROM dbo.PowerAcAchievement;
