-- Removes only dbo.PowerAcAchievement (created by 005_power_ac_achievement_deploy.sql).
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.PowerAcAchievement', N'U') IS NOT NULL
    DROP TABLE dbo.PowerAcAchievement;
