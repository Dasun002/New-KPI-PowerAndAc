IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [dbo].[BbAnwKpi] (
    [id] int NOT NULL IDENTITY,
    [network_engineer_kpi] nvarchar(200) NOT NULL,
    [division] nvarchar(100) NULL,
    [section] nvarchar(50) NULL,
    [kpi_percent] decimal(6,2) NULL,
    CONSTRAINT [PK_BbAnwKpi] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[EmailRecipients] (
    [id] int NOT NULL IDENTITY,
    [email] nvarchar(50) NOT NULL,
    [v] tinyint NOT NULL,
    CONSTRAINT [PK_EmailRecipients] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[finaldatatables] (
    [id] int NOT NULL IDENTITY,
    [perspectives] nvarchar(50) NOT NULL,
    [strategicObjectives] nvarchar(50) NOT NULL,
    [keyPerformanceIndicators] nvarchar(100) NOT NULL,
    [unit] nvarchar(50) NOT NULL,
    [descriptionOfKPI] nvarchar(50) NOT NULL,
    [weightage] decimal(10,4) NOT NULL,
    [pointsApplicable] int NOT NULL,
    [createdAt] nvarchar(50) NOT NULL,
    [updatedAt] nvarchar(50) NOT NULL,
    [month] tinyint NOT NULL,
    [year] smallint NOT NULL,
    CONSTRAINT [PK_finaldatatables] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[IpNwOpKpi] (
    [id] int NOT NULL IDENTITY,
    [network_engineer_kpi] nvarchar(255) NULL,
    [division] nvarchar(255) NULL,
    [section] nvarchar(50) NULL,
    [kpi_percent] float NULL,
    [updated_at] datetime2 NULL,
    CONSTRAINT [PK_IpNwOpKpi] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[kpitowertable] (
    [id] int NOT NULL IDENTITY,
    [no] tinyint NOT NULL,
    [responsibility] nvarchar(100) NOT NULL,
    [frequency] nvarchar(50) NOT NULL,
    [weightage] nvarchar(50) NOT NULL,
    [kpi] nvarchar(50) NOT NULL,
    [month] tinyint NULL,
    [year] smallint NULL,
    [createdAt] nvarchar(max) NULL,
    [updatedAt] nvarchar(max) NULL,
    [v] tinyint NOT NULL,
    CONSTRAINT [PK_kpitowertable] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[mtncroutinetable1] (
    [id] int NOT NULL IDENTITY,
    [no] tinyint NOT NULL,
    [kpi] nvarchar(max) NOT NULL,
    [target] nvarchar(max) NOT NULL,
    [calculation] nvarchar(max) NOT NULL,
    [platform] nvarchar(max) NOT NULL,
    [responsibleDGM] nvarchar(max) NOT NULL,
    [definedOLADetails] nvarchar(max) NOT NULL,
    [dataSources] nvarchar(max) NOT NULL,
    [createdAt] nvarchar(max) NULL,
    [updatedAt] nvarchar(max) NULL,
    [v] tinyint NOT NULL,
    CONSTRAINT [PK_mtncroutinetable1] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[OtnOp1] (
    [Id] int NOT NULL IDENTITY,
    [NetworkEngineerKpi] nvarchar(255) NOT NULL,
    [Division] nvarchar(100) NULL,
    [Section] nvarchar(100) NULL,
    [KpiPercent] decimal(6,3) NULL,
    CONSTRAINT [PK_OtnOp1] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[OtnOp2] (
    [Id] int NOT NULL IDENTITY,
    [NetworkEngineerKpi] nvarchar(255) NOT NULL,
    [Division] nvarchar(100) NULL,
    [Section] nvarchar(100) NULL,
    [KpiPercent] decimal(6,3) NULL,
    CONSTRAINT [PK_OtnOp2] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[OverallKpiResult] (
    [Id] int NOT NULL IDENTITY,
    [KpiCode] nvarchar(50) NULL,
    [KpiDefinitionId] int NOT NULL,
    [KpiName] nvarchar(255) NULL,
    [Platform] nvarchar(100) NULL,
    [AreaCode] nvarchar(50) NOT NULL,
    [TargetValue] decimal(18,4) NULL,
    [AchievedValue] decimal(10,4) NOT NULL,
    [PointsApplicable] decimal(18,4) NOT NULL,
    [PointsAchieved] decimal(18,4) NOT NULL,
    [OverallKpiValuePercent] decimal(10,4) NOT NULL,
    [Month] tinyint NOT NULL,
    [Year] smallint NOT NULL,
    [CalculatedAt] datetime2 NULL,
    CONSTRAINT [PK_OverallKpiResult] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[Page] (
    [PageId] tinyint NOT NULL,
    [PageCode] nvarchar(50) NOT NULL,
    [PageName] nvarchar(100) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Page] PRIMARY KEY ([PageId])
);
GO

CREATE TABLE [regiondata] (
    [id] int NOT NULL IDENTITY,
    [region] nvarchar(max) NOT NULL,
    [province] nvarchar(max) NOT NULL,
    [network_engineer] nvarchar(max) NOT NULL,
    [lea_code] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_regiondata] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[Roles] (
    [RoleId] int NOT NULL IDENTITY,
    [RoleName] nvarchar(50) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
);
GO

CREATE TABLE [rtom_area_lookup] (
    [area_code] nvarchar(450) NOT NULL,
    [display_name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_rtom_area_lookup] PRIMARY KEY ([area_code])
);
GO

CREATE TABLE [dbo].[ServiceFulfilmentKpi] (
    [id] int NOT NULL IDENTITY,
    [kpi] nvarchar(max) NOT NULL,
    [target] nvarchar(max) NOT NULL,
    [calculation] nvarchar(max) NOT NULL,
    [platform] nvarchar(max) NOT NULL,
    [responsibledgm] nvarchar(max) NOT NULL,
    [definedoladetails] nvarchar(max) NOT NULL,
    [weightage] int NOT NULL,
    [datasources] nvarchar(max) NOT NULL,
    [month] tinyint NOT NULL,
    [year] smallint NOT NULL,
    [updatedAt] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ServiceFulfilmentKpi] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[tmtable1] (
    [id] int NOT NULL IDENTITY,
    [no] tinyint NULL,
    [kpi] nvarchar(max) NOT NULL,
    [target] nvarchar(max) NULL,
    [calculation] nvarchar(max) NULL,
    [platform] nvarchar(max) NULL,
    [responsibleDGM] nvarchar(max) NULL,
    [definedOLADetails] nvarchar(max) NULL,
    [dataSources] nvarchar(max) NULL,
    [createdAt] nvarchar(max) NULL,
    [updatedAt] nvarchar(max) NULL,
    [v] tinyint NULL,
    CONSTRAINT [PK_tmtable1] PRIMARY KEY ([id])
);
GO

CREATE TABLE [dbo].[BbAnwKpiNode] (
    [id] int NOT NULL IDENTITY,
    [bb_anw_kpi_id] int NOT NULL,
    [node_code] nvarchar(50) NOT NULL,
    [unavailable_minutes] int NULL,
    [total_minutes] bigint NULL,
    [total_nodes] int NULL,
    [month] tinyint NOT NULL,
    [year] smallint NOT NULL,
    CONSTRAINT [PK_BbAnwKpiNode] PRIMARY KEY ([id]),
    CONSTRAINT [FK_BbAnwKpiNode_BbAnwKpi_bb_anw_kpi_id] FOREIGN KEY ([bb_anw_kpi_id]) REFERENCES [dbo].[BbAnwKpi] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[IpNwOpKpiMetrics] (
    [id] int NOT NULL IDENTITY,
    [ip_nw_op_kpi_id] int NOT NULL,
    [area_code] nvarchar(50) NOT NULL,
    [unavailable_minutes] int NULL,
    [total_minutes] int NULL,
    [total_nodes] int NULL,
    [month] tinyint NOT NULL,
    [year] smallint NOT NULL,
    CONSTRAINT [PK_IpNwOpKpiMetrics] PRIMARY KEY ([id]),
    CONSTRAINT [FK_IpNwOpKpiMetrics_IpNwOpKpi] FOREIGN KEY ([ip_nw_op_kpi_id]) REFERENCES [dbo].[IpNwOpKpi] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[OtnOp1Metrics] (
    [Id] int NOT NULL IDENTITY,
    [OtnOp1Id] int NOT NULL,
    [Site] nvarchar(20) NOT NULL,
    [UnavailableMinutes] int NOT NULL,
    [TotalMinutes] int NOT NULL,
    [TotalNodes] int NOT NULL,
    [Year] smallint NOT NULL,
    [Month] tinyint NOT NULL,
    CONSTRAINT [PK_OtnOp1Metrics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Otn1M_Otn1] FOREIGN KEY ([OtnOp1Id]) REFERENCES [dbo].[OtnOp1] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[OtnOp2Metrics] (
    [Id] int NOT NULL IDENTITY,
    [OtnOp2Id] int NOT NULL,
    [Site] nvarchar(20) NOT NULL,
    [TotalFailedLinks] int NOT NULL,
    [LinksSlaNotViolated] int NOT NULL,
    [Year] smallint NOT NULL,
    [Month] tinyint NOT NULL,
    CONSTRAINT [PK_OtnOp2Metrics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Otn2M_Otn2] FOREIGN KEY ([OtnOp2Id]) REFERENCES [dbo].[OtnOp2] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[Users] (
    [UserId] int NOT NULL IDENTITY,
    [ServiceId] nvarchar(20) NOT NULL,
    [Email] nvarchar(150) NULL,
    [Name] nvarchar(150) NOT NULL,
    [RoleId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [LastLogin] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([RoleId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [dbo].[ServiceFulfilmentKpiMetrics] (
    [id] int NOT NULL IDENTITY,
    [ServiceFulfilmentKpiId] int NOT NULL,
    [area_code] nvarchar(50) NOT NULL,
    [kpi_value] decimal(18,2) NULL,
    [month] tinyint NOT NULL,
    [year] smallint NOT NULL,
    CONSTRAINT [PK_ServiceFulfilmentKpiMetrics] PRIMARY KEY ([id]),
    CONSTRAINT [FK_ServiceFulfilmentKpiMetrics_ServiceFulfilmentKpi] FOREIGN KEY ([ServiceFulfilmentKpiId]) REFERENCES [dbo].[ServiceFulfilmentKpi] ([id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[PlatformKpiAssignment] (
    [AssignmentId] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PageId] tinyint NOT NULL,
    [AssignedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PlatformKpiAssignment] PRIMARY KEY ([AssignmentId]),
    CONSTRAINT [FK_PlatformKpiAssignment_Page_PageId] FOREIGN KEY ([PageId]) REFERENCES [dbo].[Page] ([PageId]) ON DELETE CASCADE,
    CONSTRAINT [FK_PlatformKpiAssignment_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[UserPageAccess] (
    [UserId] int NOT NULL,
    [PageId] tinyint NOT NULL,
    CONSTRAINT [PK_UserPageAccess] PRIMARY KEY ([UserId], [PageId]),
    CONSTRAINT [FK_UserPageAccess_Page_PageId] FOREIGN KEY ([PageId]) REFERENCES [dbo].[Page] ([PageId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserPageAccess_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [UQ_BbAnwKpiNode_Row] ON [dbo].[BbAnwKpiNode] ([bb_anw_kpi_id], [node_code], [month], [year]);
GO

CREATE UNIQUE INDEX [UQ_IpNwOpKpiMetrics] ON [dbo].[IpNwOpKpiMetrics] ([ip_nw_op_kpi_id], [area_code], [month], [year]);
GO

CREATE UNIQUE INDEX [UQ_Otn1M] ON [dbo].[OtnOp1Metrics] ([OtnOp1Id], [Site], [Year], [Month]);
GO

CREATE UNIQUE INDEX [UQ_Otn2M] ON [dbo].[OtnOp2Metrics] ([OtnOp2Id], [Site], [Year], [Month]);
GO

CREATE UNIQUE INDEX [UQ_OverallKpiResult_Row] ON [dbo].[OverallKpiResult] ([KpiDefinitionId], [AreaCode], [Month], [Year]);
GO

CREATE UNIQUE INDEX [IX_Page_PageCode] ON [dbo].[Page] ([PageCode]);
GO

CREATE INDEX [IX_PlatformKpiAssignment_PageId] ON [dbo].[PlatformKpiAssignment] ([PageId]);
GO

CREATE UNIQUE INDEX [IX_PlatformKpiAssignment_UserId_PageId] ON [dbo].[PlatformKpiAssignment] ([UserId], [PageId]);
GO

CREATE UNIQUE INDEX [IX_Roles_RoleName] ON [dbo].[Roles] ([RoleName]);
GO

CREATE UNIQUE INDEX [UQ_ServiceFulfilmentKpiMetrics_Row] ON [dbo].[ServiceFulfilmentKpiMetrics] ([ServiceFulfilmentKpiId], [area_code], [month], [year]);
GO

CREATE INDEX [IX_UserPageAccess_PageId] ON [dbo].[UserPageAccess] ([PageId]);
GO

CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]) WHERE [Email] IS NOT NULL;
GO

CREATE INDEX [IX_Users_RoleId] ON [dbo].[Users] ([RoleId]);
GO

CREATE UNIQUE INDEX [IX_Users_ServiceId] ON [dbo].[Users] ([ServiceId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260217083321_InitialAuth', N'8.0.4');
GO

COMMIT;
GO

