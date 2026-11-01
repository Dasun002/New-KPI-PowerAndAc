using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // =========================
        // AUTH & USERS
        // =========================
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Page> Pages { get; set; } = null!;
        public DbSet<UserPageAccess> UserPageAccess { get; set; } = null!;
        public DbSet<PlatformKpiAssignment> PlatformKpiAssignments { get; set; } = null!;

        // =========================
        // EMAILS
        // =========================
        public DbSet<EmailRecipient> EmailRecipients { get; set; } = null!;

        // =========================
        // REGION / AREA
        // =========================
        public DbSet<RegionData> RegionData { get; set; } = null!;
        public DbSet<RtomArea> RtomArea { get; set; } = null!;

        // =========================
        // MAINTENANCE ROUTINE
        // =========================
        public DbSet<MtncRoutine> MtncRoutines { get; set; } = null!;

        // =========================
        // MAINTENANCE TABLE DATA
        // =========================
        public DbSet<MsanMtcData> MsanMtcData { get; set; } = null!;
        public DbSet<TowerMtcData> TowerMtcData { get; set; } = null!;
        public DbSet<SlbnMtcData> SlbnMtcData { get; set; } = null!;
        public DbSet<IpnwMtcData> IpnwMtcData { get; set; } = null!;
        

        // =========================
        // KPI DEFINITIONS
        // =========================
        public DbSet<KpiDefinition> KpiDefinitions { get; set; } = null!;
        public DbSet<OverallKpiResult> OverallKpiResults { get; set; } = null!;

        // =========================
        // FORMS (2025)
        // =========================
        public DbSet<ServiceFulfilmentKpi> ServiceFulfilmentKpis { get; set; } = null!;
        public DbSet<ServiceFulfilmentKpiMetric> ServiceFulfilmentKpiMetrics { get; set; } = null!;



        // =========================
        // BB&ANW (FORM 7 renamed)
        // =========================
        public DbSet<BbAnwKpi> BbAnwKpis { get; set; } = null!;
        public DbSet<BbAnwKpiNode> BbAnwKpiNodes { get; set; } = null!;


        // =========================
        // TM ACTIVITY PLAN
        // =========================
        public DbSet<TmActivity1> TmActivity1 { get; set; } = null!;


        // =========================
        // IP NW OP KPI (FORM 6)
        // =========================
        public DbSet<IpNwOpKpi> IpNwOpKpis { get; set; } = null!;
        public DbSet<IpNwOpKpiMetric> IpNwOpKpiMetrics { get; set; } = null!;

        // =========================
        // ENTERPRISE KPI & OTHER KPI
        // =========================
        public DbSet<EnterpriseKpi> EnterpriseKpis { get; set; } = null!;
        public DbSet<EnterpriseKpiMetric> EnterpriseKpiMetrics { get; set; } = null!;
        public DbSet<EnterpriseTarget> EnterpriseTargets { get; set; } = null!;
        public DbSet<OtherOperatorKpi> OtherOperatorKpis { get; set; } = null!;
        public DbSet<OtherOperatorKpiMetric> OtherOperatorKpiMetrics { get; set; } = null!;
        public DbSet<OtherOperatorTarget> OtherOperatorTargets { get; set; } = null!;
        public DbSet<OtherKpi> OtherKpis { get; set; } = null!;
        public DbSet<OtherKpiMetric> OtherKpiMetrics { get; set; } = null!;
      // TELEMETRY / POWER & AC
      public DbSet<Telemetry> Telemetry { get; set; } = null!;
      public DbSet<PowerAndAC> PowerAndAC { get; set; } = null!;

        // =========================
        // POWER & AC KPI (four-component model, see PowerAcModelConfiguration)
        // =========================
        public DbSet<backend.Models.PowerAc.PowerAcComponent> PowerAcComponents { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcComponentWeight> PowerAcComponentWeights { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcEngineerGroup> PowerAcEngineerGroups { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcGroupUserAssignment> PowerAcGroupUserAssignments { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcAreaAllocation> PowerAcAreaAllocations { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcComponentResult> PowerAcComponentResults { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcCalculationSetting> PowerAcCalculationSettings { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcKpiResult> PowerAcKpiResults { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcKpiComponentScore> PowerAcKpiComponentScores { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcComponentVersion> PowerAcComponentVersions { get; set; } = null!;
        public DbSet<backend.Models.PowerAc.PowerAcAuditLog> PowerAcAuditLogs { get; set; } = null!;

        // =========================
        // AGED NETWORK FAILURE METRICS
        // =========================
       //public DbSet<AgedNetworkFailureMetric> AgedNetworkFailureMetrics { get; set; } = null!;

        //OTNOP1 AND OTNOP2
        public DbSet<OtnOp1> OtnOp1 { get; set; } = null!;
        public DbSet<OtnOp1Metrics> OtnOp1Metrics { get; set; } = null!;
        public DbSet<OtnOp2> OtnOp2 { get; set; } = null!;
        public DbSet<OtnOp2Metrics> OtnOp2Metrics { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // =========================
            // AUTHENTICATION & AUTHORIZATION
            // =========================

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles", "dbo");
                entity.HasKey(e => e.RoleId);
                entity.Property(e => e.RoleName).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.RoleName).IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users", "dbo");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => e.ServiceId).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(150);
                entity.HasIndex(e => e.Email).IsUnique();

                entity.HasOne(d => d.Role)
                    .WithMany()
                    .HasForeignKey(d => d.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Page>(entity =>
            {
                entity.ToTable("Page", "dbo");
                entity.HasKey(e => e.PageId);
                entity.Property(e => e.PageCode).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.PageCode).IsUnique();
                entity.Property(e => e.PageName).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<UserPageAccess>(entity =>
            {
                entity.ToTable("UserPageAccess", "dbo");
                entity.HasKey(e => new { e.UserId, e.PageId });

                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Page)
                    .WithMany()
                    .HasForeignKey(d => d.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PlatformKpiAssignment>(entity =>
            {
                entity.ToTable("PlatformKpiAssignment", "dbo");
                entity.HasKey(e => e.AssignmentId);

                entity.HasIndex(e => new { e.UserId, e.PageId }).IsUnique();

                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Page)
                    .WithMany()
                    .HasForeignKey(d => d.PageId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // EMAIL RECIPIENTS
            modelBuilder.Entity<EmailRecipient>(entity =>
            {
                entity.ToTable("EmailRecipients", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd(); // Identity column - auto-generated by database

                entity.Property(x => x.Email)
                    .HasColumnName("email")
                    .HasMaxLength(50)  // Match database: nvarchar(50)
                    .IsRequired();

                entity.Property(x => x.V)
                    .HasColumnName("v")
                    .IsRequired();
            });

            // TM ACTIVITY PLAN
            modelBuilder.Entity<TmActivity1>(entity =>
            {
                entity.ToTable("tmtable1", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.No).HasColumnName("no");
                entity.Property(x => x.Kpi).HasColumnName("kpi");
                entity.Property(x => x.Target).HasColumnName("target");
                entity.Property(x => x.Calculation).HasColumnName("calculation");
                entity.Property(x => x.CreatedAt).HasColumnName("createdAt");
                entity.Property(x => x.UpdatedAt).HasColumnName("updatedAt");
            });


            // MAINTENANCE ROUTINE
            modelBuilder.Entity<MtncRoutine>(entity =>
            {
                entity.ToTable("mtncroutinetable1", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd(); // Identity column - auto-generated by database
                entity.Property(x => x.No).HasColumnName("no");
                entity.Property(x => x.Kpi).HasColumnName("kpi");
                entity.Property(x => x.Target).HasColumnName("target");
                entity.Property(x => x.Calculation).HasColumnName("calculation");
                entity.Property(x => x.Platform).HasColumnName("platform");
                entity.Property(x => x.ResponsibleDGM).HasColumnName("responsibleDGM");
                entity.Property(x => x.DefinedOLADetails).HasColumnName("definedOLADetails");
                entity.Property(x => x.DataSources).HasColumnName("dataSources");
                entity.Property(x => x.CreatedAt).HasColumnName("createdAt");
                entity.Property(x => x.UpdatedAt).HasColumnName("updatedAt");
                entity.Property(x => x.V).HasColumnName("v");
            });

            // IP NW OP KPI
            // IP NW OP KPI (FORM 6) - UPDATED
            modelBuilder.Entity<IpNwOpKpi>(entity =>
            {
                entity.ToTable("IpNwOpKpi", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("id")
                      .ValueGeneratedOnAdd(); // ✅ INT IDENTITY

                entity.Property(x => x.NetworkEngineerKpi)
                      .HasColumnName("network_engineer_kpi")
                      .HasMaxLength(255);

                entity.Property(x => x.Division)
                      .HasColumnName("division")
                      .HasMaxLength(255);

                entity.Property(x => x.Section)
                      .HasColumnName("section")
                      .HasMaxLength(50);

                entity.Property(x => x.KpiPercent)
                      .HasColumnName("kpi_percent");

                entity.Property(x => x.UpdatedAt)
                      .HasColumnName("updated_at");
            });

            modelBuilder.Entity<IpNwOpKpiMetric>(entity =>
            {
                entity.ToTable("IpNwOpKpiMetrics", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("id")
                      .ValueGeneratedOnAdd(); // ✅ INT IDENTITY

                entity.Property(x => x.IpNwOpKpiId)
                      .HasColumnName("ip_nw_op_kpi_id"); // ✅ INT FK (NO MaxLength)

                entity.Property(x => x.AreaCode)
                      .HasColumnName("area_code")
                      .HasMaxLength(50)
                      .IsRequired();

                entity.Property(x => x.UnavailableMinutes).HasColumnName("unavailable_minutes");
                entity.Property(x => x.TotalMinutes).HasColumnName("total_minutes");
                entity.Property(x => x.TotalNodes).HasColumnName("total_nodes");

                // ✅ NEW columns in metrics table
                entity.Property(x => x.Month).HasColumnName("month");
                entity.Property(x => x.Year).HasColumnName("year");

                entity.HasOne(x => x.IpNwOpKpi)
                      .WithMany(k => k.Metrics)
                      .HasForeignKey(x => x.IpNwOpKpiId)
                      .HasConstraintName("FK_IpNwOpKpiMetrics_IpNwOpKpi")
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ Match DB unique constraint: (ip_nw_op_kpi_id, area_code, month, year)
                entity.HasIndex(x => new { x.IpNwOpKpiId, x.AreaCode, x.Month, x.Year })
                      .IsUnique()
                      .HasDatabaseName("UQ_IpNwOpKpiMetrics");
            });

            // =========================
            // BB&ANW KPI (Form7 renamed tables)
            // =========================
            modelBuilder.Entity<BbAnwKpi>(entity =>
            {
                entity.ToTable("BbAnwKpi", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                entity.Property(x => x.NetworkEngineerKpi).HasColumnName("network_engineer_kpi").HasMaxLength(200);
                entity.Property(x => x.Division).HasColumnName("division").HasMaxLength(100);
                entity.Property(x => x.Section).HasColumnName("section").HasMaxLength(50);

                entity.Property(x => x.KpiPercent)
                      .HasColumnName("kpi_percent")
                      .HasColumnType("decimal(6,2)"); // ✅ match DB type

                entity.HasMany(x => x.Nodes)
                      .WithOne(n => n.Kpi)
                      .HasForeignKey(n => n.BbAnwKpiId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BbAnwKpiNode>(entity =>
            {
                entity.ToTable("BbAnwKpiNode", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

                entity.Property(x => x.BbAnwKpiId).HasColumnName("bb_anw_kpi_id");
                entity.Property(x => x.NodeCode).HasColumnName("node_code").HasMaxLength(50);

                entity.Property(x => x.UnavailableMinutes).HasColumnName("unavailable_minutes");
                entity.Property(x => x.TotalMinutes).HasColumnName("total_minutes");
                entity.Property(x => x.TotalNodes).HasColumnName("total_nodes");

                entity.Property(x => x.Month).HasColumnName("month");
                entity.Property(x => x.Year).HasColumnName("year");

                // ✅ prevent duplicates
                entity.HasIndex(x => new { x.BbAnwKpiId, x.NodeCode, x.Month, x.Year })
                      .IsUnique()
                      .HasDatabaseName("UQ_BbAnwKpiNode_Row");
            });

            // KPI DEFINITIONS
            modelBuilder.Entity<KpiDefinition>(entity =>
            {
                entity.ToTable("KpiDefinition", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(x => x.Perspectives).HasColumnName("perspectives").HasMaxLength(50).IsRequired();
                entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired(false);
                entity.Property(x => x.EngineerResponsible).HasColumnName("engineerResponsible").HasMaxLength(100).IsRequired(false);
                entity.Property(x => x.StrategicObjectives).HasColumnName("strategicObjectives").HasMaxLength(50).IsRequired();
                entity.Property(x => x.KeyPerformanceIndicators).HasColumnName("keyPerformanceIndicators").HasMaxLength(100).IsRequired();
                entity.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(50).IsRequired();
                entity.Property(x => x.DescriptionOfKPI).HasColumnName("descriptionOfKPI").HasMaxLength(50).IsRequired();
                entity.Property(x => x.Weightage).HasColumnName("weightage").HasColumnType("decimal(10,4)").IsRequired();
                entity.Property(x => x.PointsApplicable).HasColumnName("pointsApplicable").IsRequired();
                entity.Property(x => x.TotalPoints).HasColumnName("totalPoints").IsRequired();
                entity.Property(x => x.CreatedAt).HasColumnName("createdAt").HasMaxLength(50).IsRequired(false);
                entity.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasMaxLength(50).IsRequired(false);
            });

            modelBuilder.Entity<OverallKpiResult>(entity =>
            {
                entity.ToTable("OverallKpiResult", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();

                entity.Property(x => x.KpiCode).HasColumnName("KpiCode").HasMaxLength(50);
                entity.Property(x => x.KpiDefinitionId).HasColumnName("KpiDefinitionId");
                entity.Property(x => x.KpiName).HasColumnName("KpiName").HasMaxLength(255);
                entity.Property(x => x.Platform).HasColumnName("Platform").HasMaxLength(100);
                entity.Property(x => x.AreaCode).HasColumnName("AreaCode").HasMaxLength(50).IsRequired();
                entity.Property(x => x.TargetValue).HasColumnName("TargetValue").HasColumnType("decimal(18,4)");
                entity.Property(x => x.AchievedKpi).HasColumnName("AchievedValue").HasColumnType("decimal(10,4)");
                entity.Property(x => x.MaximumPointsPerKpi).HasColumnName("PointsApplicable").HasColumnType("decimal(18,4)");
                entity.Property(x => x.PointsAchieved).HasColumnName("PointsAchieved").HasColumnType("decimal(18,4)");
                entity.Property(x => x.Month).HasColumnName("Month");
                entity.Property(x => x.Year).HasColumnName("Year");
                entity.Property(x => x.CalculatedAt).HasColumnName("CalculatedAt");

                entity.HasIndex(x => new { x.KpiDefinitionId, x.AreaCode, x.Month, x.Year })
                      .IsUnique()
                      .HasDatabaseName("UQ_OverallKpiResult_Row");
            });

            //servicefullilment
            modelBuilder.Entity<ServiceFulfilmentKpi>(entity =>
            {
                entity.ToTable("ServiceFulfilmentKpi", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("id")
                      .ValueGeneratedOnAdd(); // ✅ INT IDENTITY

                entity.Property(x => x.Kpi).HasColumnName("kpi");
                entity.Property(x => x.Target).HasColumnName("target");
                entity.Property(x => x.Calculation).HasColumnName("calculation");
                entity.Property(x => x.Platform).HasColumnName("platform");
                entity.Property(x => x.ResponsibleDgm).HasColumnName("responsibledgm");
                entity.Property(x => x.DefineDoladetails).HasColumnName("definedoladetails");
                entity.Property(x => x.Weightage).HasColumnName("weightage");
                entity.Property(x => x.DataSources).HasColumnName("datasources");

                entity.Property(x => x.Month).HasColumnName("month");
                entity.Property(x => x.Year).HasColumnName("year");
                entity.Property(x => x.UpdatedAt).HasColumnName("updatedAt");
            });
            modelBuilder.Entity<ServiceFulfilmentKpiMetric>(entity =>
            {
                entity.ToTable("ServiceFulfilmentKpiMetrics", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("id")
                      .ValueGeneratedOnAdd(); // ✅ INT IDENTITY

                entity.Property(x => x.ServiceFulfilmentKpiId)
                      .HasColumnName("ServiceFulfilmentKpiId"); // ✅ INT FK (no MaxLength)

                entity.Property(x => x.AreaCode)
                      .HasColumnName("area_code")
                      .HasMaxLength(50);

                entity.Property(x => x.KpiValue)
                     .HasColumnName("kpi_value")
                     .HasColumnType("decimal(18,2)");
                entity.Property(x => x.Month).HasColumnName("month");
                entity.Property(x => x.Year).HasColumnName("year");

                entity.HasOne(x => x.ServiceFulfilmentKpi)
                      .WithMany(k => k.Metrics)
                      .HasForeignKey(x => x.ServiceFulfilmentKpiId)
                      .HasConstraintName("FK_ServiceFulfilmentKpiMetrics_ServiceFulfilmentKpi")
                      .OnDelete(DeleteBehavior.Cascade);

                // ✅ Important: prevent duplicates for same KPI+area+month+year
                entity.HasIndex(x => new { x.ServiceFulfilmentKpiId, x.AreaCode, x.Month, x.Year })
                      .IsUnique()
                      .HasDatabaseName("UQ_ServiceFulfilmentKpiMetrics_Row");
            });


            //OTNOP1 AND OTNOP2 
            // =========================
            // OtnOp1 (Form8) + Metrics
            // =========================
            modelBuilder.Entity<OtnOp1>(entity =>
            {
                entity.ToTable("OtnOp1", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("Id")
                      .ValueGeneratedOnAdd(); // INT IDENTITY

                entity.Property(x => x.NetworkEngineerKpi)
                      .HasColumnName("NetworkEngineerKpi")
                      .HasMaxLength(255)
                      .IsRequired();

                entity.Property(x => x.Division)
                      .HasColumnName("Division")
                      .HasMaxLength(100);

                entity.Property(x => x.Section)
                      .HasColumnName("Section")
                      .HasMaxLength(100);

                entity.Property(x => x.KpiPercent)
                      .HasColumnName("KpiPercent")
                      .HasColumnType("decimal(6,3)");
            });

            modelBuilder.Entity<OtnOp1Metrics>(entity =>
            {
                entity.ToTable("OtnOp1Metrics", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("Id")
                      .ValueGeneratedOnAdd();

                entity.Property(x => x.OtnOp1Id)
                      .HasColumnName("OtnOp1Id")
                      .IsRequired();

                entity.Property(x => x.Site)
                      .HasColumnName("Site")
                      .HasMaxLength(20)
                      .IsRequired();

                entity.Property(x => x.UnavailableMinutes).HasColumnName("UnavailableMinutes");
                entity.Property(x => x.TotalMinutes).HasColumnName("TotalMinutes");
                entity.Property(x => x.TotalNodes).HasColumnName("TotalNodes");

                entity.Property(x => x.Year).HasColumnName("Year");
                entity.Property(x => x.Month).HasColumnName("Month");

                entity.HasOne(x => x.OtnOp1)
                      .WithMany(k => k.Metrics)
                      .HasForeignKey(x => x.OtnOp1Id)
                      .HasConstraintName("FK_Otn1M_Otn1")
                      .OnDelete(DeleteBehavior.Cascade);

                // matches: UQ_Otn1M UNIQUE (OtnOp1Id, Site, Year, Month)
                entity.HasIndex(x => new { x.OtnOp1Id, x.Site, x.Year, x.Month })
                      .IsUnique()
                      .HasDatabaseName("UQ_Otn1M");
            });


            // =========================
            // OtnOp2 (Form9) + Metrics
            // =========================
            modelBuilder.Entity<OtnOp2>(entity =>
            {
                entity.ToTable("OtnOp2", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("Id")
                      .ValueGeneratedOnAdd();

                entity.Property(x => x.NetworkEngineerKpi)
                      .HasColumnName("NetworkEngineerKpi")
                      .HasMaxLength(255)
                      .IsRequired();

                entity.Property(x => x.Division)
                      .HasColumnName("Division")
                      .HasMaxLength(100);

                entity.Property(x => x.Section)
                      .HasColumnName("Section")
                      .HasMaxLength(100);

                entity.Property(x => x.KpiPercent)
                      .HasColumnName("KpiPercent")
                      .HasColumnType("decimal(6,3)");
            });

            modelBuilder.Entity<OtnOp2Metrics>(entity =>
            {
                entity.ToTable("OtnOp2Metrics", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id)
                      .HasColumnName("Id")
                      .ValueGeneratedOnAdd();

                entity.Property(x => x.OtnOp2Id)
                      .HasColumnName("OtnOp2Id")
                      .IsRequired();

                entity.Property(x => x.Site)
                      .HasColumnName("Site")
                      .HasMaxLength(20)
                      .IsRequired();

                entity.Property(x => x.TotalFailedLinks).HasColumnName("TotalFailedLinks");
                entity.Property(x => x.LinksSlaNotViolated).HasColumnName("LinksSlaNotViolated");

                entity.Property(x => x.Year).HasColumnName("Year");
                entity.Property(x => x.Month).HasColumnName("Month");

                entity.HasOne(x => x.OtnOp2)
                      .WithMany(k => k.Metrics)
                      .HasForeignKey(x => x.OtnOp2Id)
                      .HasConstraintName("FK_Otn2M_Otn2")
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.OtnOp2Id, x.Site, x.Year, x.Month })
                      .IsUnique()
                      .HasDatabaseName("UQ_Otn2M");
            });





            // =========================
            // ENTERPRISE KPI
            // =========================
            modelBuilder.Entity<EnterpriseKpi>(entity =>
            {
                entity.ToTable("EnterpriseKpi", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.NetworkEngineerKpi).HasColumnName("network_engineer_kpi").HasMaxLength(255).IsRequired();
                entity.Property(x => x.Division).HasColumnName("division").HasMaxLength(100);
                entity.Property(x => x.KpiPercent).HasColumnName("kpi_percent").HasColumnType("decimal(6,3)");
            });

            modelBuilder.Entity<EnterpriseKpiMetric>(entity =>
            {
                entity.ToTable("EnterpriseKpiMetrics", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.EnterpriseKpiId).HasColumnName("EnterpriseKpiId").IsRequired();
                entity.Property(x => x.Site).HasColumnName("Site").HasMaxLength(50).IsRequired();
                entity.Property(x => x.KpiValue).HasColumnName("KpiValue").HasColumnType("decimal(18,4)");
                entity.Property(x => x.Month).HasColumnName("month");
                entity.Property(x => x.Year).HasColumnName("year");
                entity.Property(x => x.CreatedAt).HasColumnName("CreatedAt");
                entity.Property(x => x.UpdatedAt).HasColumnName("UpdatedAt");
                entity.Property(x => x.Target).HasColumnName("Target").HasMaxLength(100);

                entity.HasOne(x => x.EnterpriseKpi)
                      .WithMany()
                      .HasForeignKey(x => x.EnterpriseKpiId)
                      .HasConstraintName("FK_EnterpriseKpiMetrics_EnterpriseKpi")
                      .OnDelete(DeleteBehavior.Cascade);

                    entity.HasIndex(x => new { x.EnterpriseKpiId, x.Site, x.Month, x.Year })
                      .IsUnique()
                      .HasDatabaseName("UQ_EnterpriseKpiMetrics_Row");
            });

            modelBuilder.Entity<EnterpriseTarget>(entity =>
            {
                entity.ToTable("EnterpriseTargets", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.EnterpriseKpiId).HasColumnName("EnterpriseKpiId").IsRequired();
                entity.Property(x => x.Section).HasColumnName("Section").HasMaxLength(100);
                entity.Property(x => x.Month).HasColumnName("Month");
                entity.Property(x => x.Year).HasColumnName("Year");

                entity.HasOne(x => x.EnterpriseKpi)
                      .WithMany()
                      .HasForeignKey(x => x.EnterpriseKpiId)
                      .HasConstraintName("FK_EnterpriseTargets_EnterpriseKpi")
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // OTHER KPI
            // =========================
            modelBuilder.Entity<OtherKpi>(entity =>
            {
                entity.ToTable("OtherKpi", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.NetworkEngineerKpi).HasColumnName("network_engineer_kpi").HasMaxLength(255).IsRequired();
                entity.Property(x => x.Division).HasColumnName("division").HasMaxLength(100);
                entity.Property(x => x.Section).HasColumnName("section").HasMaxLength(100);
                entity.Property(x => x.KpiPercent).HasColumnName("kpi_percent").HasColumnType("decimal(6,3)");
            });

            modelBuilder.Entity<OtherKpiMetric>(entity =>
            {
                entity.ToTable("OtherKpiMetrics", "dbo");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.OtherKpiId).HasColumnName("OtherKpiId").IsRequired();
                entity.Property(x => x.AreaCode).HasColumnName("AreaCode").HasMaxLength(50).IsRequired();
                entity.Property(x => x.KpiValue).HasColumnName("KpiValue").HasColumnType("decimal(18,4)");
                entity.Property(x => x.Year).HasColumnName("year");
                entity.Property(x => x.Month).HasColumnName("month");
                entity.Property(x => x.CreatedAt).HasColumnName("createdAt");

                entity.HasOne(x => x.OtherKpi)
                      .WithMany()
                      .HasForeignKey(x => x.OtherKpiId)
                      .HasConstraintName("FK_OtherKpiMetrics_OtherKpi")
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.OtherKpiId, x.AreaCode, x.Month, x.Year })
                    .IsUnique()
                    .HasDatabaseName("UQ_OtherKpiMetrics_Row");
            });

            modelBuilder.Entity<OtherOperatorKpi>(entity =>
            {
                entity.ToTable("OtherOperatorKpi", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.NetworkEngineerKpi).HasColumnName("network_engineer_kpi").HasMaxLength(255).IsRequired();
                entity.Property(x => x.Division).HasColumnName("division").HasMaxLength(100);
                entity.Property(x => x.KpiPercent).HasColumnName("kpi_percent").HasColumnType("decimal(6,3)");
            });

            modelBuilder.Entity<OtherOperatorTarget>(entity =>
            {
                entity.ToTable("OtherOperatorTargets", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.OtherOperatorKpiId).HasColumnName("OtherOperatorKpiId").IsRequired();
                entity.Property(x => x.Section).HasColumnName("Section").HasMaxLength(100);
                entity.Property(x => x.Month).HasColumnName("Month");
                entity.Property(x => x.Year).HasColumnName("Year");

                entity.HasOne(x => x.OtherOperatorKpi)
                      .WithMany()
                      .HasForeignKey(x => x.OtherOperatorKpiId)
                      .HasConstraintName("FK_OtherOperatorTargets_OtherOperatorKpi")
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<OtherOperatorKpiMetric>(entity =>
            {
                entity.ToTable("OtherOperatorKpiMetrics", "dbo");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                entity.Property(x => x.OtherOperatorKpiId).HasColumnName("OtherOperatorKpiId").IsRequired();
                entity.Property(x => x.Site).HasColumnName("Site").HasMaxLength(100);
                entity.Property(x => x.Year).HasColumnName("Year");
                entity.Property(x => x.Month).HasColumnName("Month");
                entity.Property(x => x.KpiValue).HasColumnName("kpi_value").HasColumnType("decimal(18,4)");
                entity.Property(x => x.Target).HasColumnName("Target").HasMaxLength(100);

                entity.HasOne(x => x.OtherOperatorKpi)
                      .WithMany()
                      .HasForeignKey(x => x.OtherOperatorKpiId)
                      .HasConstraintName("FK_OtherOperatorKpiMetrics_OtherOperatorKpi")
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // AGED NETWORK FAILURE METRICS
            // =========================
           

                  // TELEMETRY
                  modelBuilder.Entity<Telemetry>(entity =>
                  {
                        entity.ToTable("Telemetry", "dbo");
                        entity.HasKey(x => x.Id);
                        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                        entity.Property(x => x.Designation).HasColumnName("Designation").HasMaxLength(100).IsRequired();
                        entity.Property(x => x.Year).HasColumnName("Year");
                        entity.Property(x => x.Month).HasColumnName("Month");
                        entity.Property(x => x.Percentage).HasColumnName("Percentage").HasColumnType("decimal(5,2)");
                        entity.Property(x => x.Node_Count).HasColumnName("Node_Count");

                        entity.HasIndex(x => new { x.Designation, x.Year, x.Month })
                                .IsUnique()
                                .HasDatabaseName("UQ_Telemetry_Designation_Year_Month");
                  });

                  // POWER AND AC
                  modelBuilder.Entity<PowerAndAC>(entity =>
                  {
                        entity.ToTable("PowerAndAC", "dbo");
                        entity.HasKey(x => x.Id);
                        entity.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
                        entity.Property(x => x.Designation).HasColumnName("Designation").HasMaxLength(100).IsRequired();
                        entity.Property(x => x.Year).HasColumnName("Year");
                        entity.Property(x => x.Month).HasColumnName("Month");
                        entity.Property(x => x.Scheduled).HasColumnName("Scheduled");
                        entity.Property(x => x.Attended).HasColumnName("Attended");
                        entity.Property(x => x.Cumulative_Sched).HasColumnName("Cumulative_Sched");
                        entity.Property(x => x.Cumulative_Achieved).HasColumnName("Cumulative_Achieved");
                        entity.Property(x => x.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);

                        entity.HasIndex(x => new { x.Designation, x.Year, x.Month })
                                .IsUnique()
                                .HasDatabaseName("UQ_PowerAndAC_Designation_Year_Month");
                  });

            // POWER & AC KPI COMPONENT TABLES
            PowerAcModelConfiguration.Configure(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }
    }
}
