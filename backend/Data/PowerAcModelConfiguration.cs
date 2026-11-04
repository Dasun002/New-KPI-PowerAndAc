/*
 * File: PowerAcModelConfiguration.cs
 * EF Core mappings for the Power & AC KPI tables.
 * Must stay in line with database/power-ac/001_power_ac_kpi_deploy.sql.
 */
using backend.Models;
using backend.Models.PowerAc;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public static class PowerAcModelConfiguration
    {
        public static void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PowerAcComponentVersion>(e =>
            {
                e.ToTable("PowerAcComponentVersion", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.TargetValue).HasColumnType("decimal(9,4)");
                e.Property(x => x.ScoringMethod).HasMaxLength(40).IsRequired();
                e.Property(x => x.ScoringParameter).HasColumnType("decimal(9,4)");
                e.Property(x => x.ApprovedBy).HasMaxLength(100);
                e.Property(x => x.ApprovalNote).HasMaxLength(500);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.UpdatedBy).HasMaxLength(100);
                e.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.ComponentId, x.EffectiveYear, x.EffectiveMonth })
                    .IsUnique().HasDatabaseName("UQ_PowerAcComponentVersion_Component_Effective");
            });

            modelBuilder.Entity<PowerAcAuditLog>(e =>
            {
                e.ToTable("PowerAcAuditLog", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
                e.Property(x => x.Action).HasMaxLength(40).IsRequired();
                e.Property(x => x.FromStatus).HasMaxLength(20);
                e.Property(x => x.ToStatus).HasMaxLength(20);
                e.Property(x => x.Comment).HasMaxLength(500);
                e.Property(x => x.ChangedBy).HasMaxLength(100);
                e.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("IX_PowerAcAuditLog_Entity");
            });

            modelBuilder.Entity<PowerAcComponent>(e =>
            {
                e.ToTable("PowerAcComponent", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(30).IsRequired();
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.TargetValue).HasColumnType("decimal(9,4)");
                e.Property(x => x.TargetUnit).HasMaxLength(20).IsRequired();
                e.Property(x => x.DataSource).HasMaxLength(30).IsRequired();
                e.Property(x => x.ScoringMethod).HasMaxLength(40).IsRequired();
                e.Property(x => x.ScoringParameter).HasColumnType("decimal(9,4)");
                e.Property(x => x.ApprovedBy).HasMaxLength(100);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.UpdatedBy).HasMaxLength(100);
                e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_PowerAcComponent_Code");
            });

            modelBuilder.Entity<PowerAcComponentWeight>(e =>
            {
                e.ToTable("PowerAcComponentWeight", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.WeightPercent).HasColumnType("decimal(7,4)");
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.ComponentId, x.EffectiveYear, x.EffectiveMonth })
                    .IsUnique().HasDatabaseName("UQ_PowerAcComponentWeight_Component_Effective");
            });

            modelBuilder.Entity<PowerAcEngineerGroup>(e =>
            {
                e.ToTable("PowerAcEngineerGroup", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(30).IsRequired();
                e.Property(x => x.WorkbookLabel).HasMaxLength(50).IsRequired();
                e.Property(x => x.WorkbookAllocationCell).HasMaxLength(10).IsRequired();
                e.Property(x => x.PmDesignation).HasMaxLength(100);
                e.Property(x => x.MappingNote).HasMaxLength(500);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.UpdatedBy).HasMaxLength(100);
                e.HasOne(x => x.RegionData).WithMany().HasForeignKey(x => x.RegionDataId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_PowerAcEngineerGroup_Code");
                e.HasIndex(x => x.WorkbookAllocationCell).IsUnique().HasDatabaseName("UQ_PowerAcEngineerGroup_Cell");
            });

            modelBuilder.Entity<PowerAcGroupUserAssignment>(e =>
            {
                e.ToTable("PowerAcGroupUserAssignment", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.AssignedBy).HasMaxLength(100);
                e.HasOne(x => x.EngineerGroup).WithMany().HasForeignKey(x => x.EngineerGroupId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.EngineerGroupId, x.UserId })
                    .IsUnique().HasDatabaseName("UQ_PowerAcGroupUserAssignment_Group_User");
            });

            modelBuilder.Entity<PowerAcAreaAllocation>(e =>
            {
                e.ToTable("PowerAcAreaAllocation", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.AllocationValue).HasColumnType("decimal(18,4)");
                e.Property(x => x.Remarks).HasMaxLength(500);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.UpdatedBy).HasMaxLength(100);
                e.HasOne(x => x.EngineerGroup).WithMany().HasForeignKey(x => x.EngineerGroupId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.EngineerGroupId, x.EffectiveYear, x.EffectiveMonth })
                    .IsUnique().HasDatabaseName("UQ_PowerAcAreaAllocation_Group_Effective");
            });

            modelBuilder.Entity<PowerAcComponentResult>(e =>
            {
                e.ToTable("PowerAcComponentResult", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.ApplicableCount).HasColumnType("decimal(18,4)");
                e.Property(x => x.CompletedCount).HasColumnType("decimal(18,4)");
                e.Property(x => x.AchievementPercent).HasColumnType("decimal(9,4)");
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Remarks).HasMaxLength(500);
                e.Property(x => x.ReviewComment).HasMaxLength(500);
                e.Property(x => x.SubmittedBy).HasMaxLength(100);
                e.Property(x => x.VerifiedBy).HasMaxLength(100);
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.Property(x => x.UpdatedBy).HasMaxLength(100);
                e.HasOne(x => x.EngineerGroup).WithMany().HasForeignKey(x => x.EngineerGroupId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.EngineerGroupId, x.Year, x.Month, x.ComponentId })
                    .IsUnique().HasDatabaseName("UQ_PowerAcComponentResult_Group_Period_Component");
                e.HasIndex(x => new { x.Year, x.Month, x.Status }).HasDatabaseName("IX_PowerAcComponentResult_Period_Status");
            });

            modelBuilder.Entity<PowerAcCalculationSetting>(e =>
            {
                e.ToTable("PowerAcCalculationSetting", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.PointsMethod).HasMaxLength(30).IsRequired();
                e.Property(x => x.CompositeTarget).HasColumnType("decimal(9,4)");
                e.Property(x => x.CreatedBy).HasMaxLength(100);
                e.HasIndex(x => new { x.EffectiveYear, x.EffectiveMonth })
                    .IsUnique().HasDatabaseName("UQ_PowerAcCalculationSetting_Effective");
            });

            modelBuilder.Entity<PowerAcKpiResult>(e =>
            {
                e.ToTable("PowerAcKpiResult", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.PeriodType).HasMaxLength(10).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.CompositeAchievement).HasColumnType("decimal(9,6)");
                e.Property(x => x.PointsApplicable).HasColumnType("decimal(18,4)");
                e.Property(x => x.AllocationValue).HasColumnType("decimal(18,4)");
                e.Property(x => x.TotalAllocation).HasColumnType("decimal(18,4)");
                e.Property(x => x.MaximumPoints).HasColumnType("decimal(18,4)");
                e.Property(x => x.PointsAchieved).HasColumnType("decimal(18,4)");
                e.Property(x => x.PointsMethod).HasMaxLength(30).IsRequired();
                e.Property(x => x.CompositeTarget).HasColumnType("decimal(9,4)");
                e.Property(x => x.CalculatedBy).HasMaxLength(100);
                e.HasOne(x => x.EngineerGroup).WithMany().HasForeignKey(x => x.EngineerGroupId).OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.ComponentScores).WithOne(x => x.KpiResult!).HasForeignKey(x => x.KpiResultId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.EngineerGroupId, x.Year, x.PeriodType, x.PeriodNumber })
                    .IsUnique().HasDatabaseName("UQ_PowerAcKpiResult_Group_Period");
            });

            modelBuilder.Entity<PowerAcKpiComponentScore>(e =>
            {
                e.ToTable("PowerAcKpiComponentScore", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.WeightPercent).HasColumnType("decimal(7,4)");
                e.Property(x => x.ScoringMethod).HasMaxLength(40).IsRequired();
                e.Property(x => x.ApplicableCount).HasColumnType("decimal(18,4)");
                e.Property(x => x.CompletedCount).HasColumnType("decimal(18,4)");
                e.Property(x => x.AchievementRatio).HasColumnType("decimal(9,6)");
                e.Property(x => x.Score).HasColumnType("decimal(9,6)");
                e.Property(x => x.WeightedScore).HasColumnType("decimal(9,6)");
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Message).HasMaxLength(500);
                e.HasOne(x => x.Component).WithMany().HasForeignKey(x => x.ComponentId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.KpiResultId, x.ComponentId })
                    .IsUnique().HasDatabaseName("UQ_PowerAcKpiComponentScore_Result_Component");
            });
        }
    }
}
