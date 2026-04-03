using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace VinhKhanhApi.Models;

public partial class VinhKhanhAudioGuideContext : DbContext
{
    public VinhKhanhAudioGuideContext()
    {
    }

    public VinhKhanhAudioGuideContext(DbContextOptions<VinhKhanhAudioGuideContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Menu> Menus { get; set; }

    public virtual DbSet<Poi> Pois { get; set; }

    public virtual DbSet<PoiSubmission> PoiSubmissions { get; set; }

    public virtual DbSet<Poilocalization> Poilocalizations { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<VisitLog> VisitLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            base.OnConfiguring(optionsBuilder);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__AdminUse__1788CCACBF85A864");

            entity.ToTable("AdminUser");

            entity.HasIndex(e => e.UserName, "UQ__AdminUse__C9F284568BBD5A3A").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Role).WithMany(p => p.AdminUsers)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__AdminUser__RoleI__3A81B327");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__AuditLog__5E548648F51DAEB6");

            entity.Property(e => e.Action).HasMaxLength(255);
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__AuditLogs__UserI__4CA06362");
        });

        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasKey(e => e.MenuId).HasName("PK__Menu__C99ED25099334990");

            entity.ToTable("Menu");

            entity.Property(e => e.MenuId).HasColumnName("MenuID");
            entity.Property(e => e.FoodName).HasMaxLength(255);
            entity.Property(e => e.Image).HasMaxLength(500);
            entity.Property(e => e.Poiid).HasColumnName("POIID");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Poi).WithMany(p => p.Menus)
                .HasForeignKey(d => d.Poiid)
                .HasConstraintName("FK__Menu__POIID__44FF419A");
        });

        modelBuilder.Entity<Poi>(entity =>
        {
            entity.HasKey(e => e.Poiid).HasName("PK__POIS__5229E33F7B148DDF");

            entity.ToTable("POIS");

            entity.Property(e => e.Poiid).HasColumnName("POIID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Radius).HasDefaultValue(50);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Thumbnail).HasMaxLength(500);
        });

        modelBuilder.Entity<PoiSubmission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId).HasName("PK__PoiSubmi__449EE1250D496B89");

            entity.Property(e => e.Poiid).HasColumnName("POIID");
            entity.Property(e => e.Status).HasDefaultValue(0);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Poi).WithMany(p => p.PoiSubmissions)
                .HasForeignKey(d => d.Poiid)
                .HasConstraintName("FK__PoiSubmis__POIID__47DBAE45");

            entity.HasOne(d => d.User).WithMany(p => p.PoiSubmissions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__PoiSubmis__UserI__48CFD27E");
        });

        modelBuilder.Entity<Poilocalization>(entity =>
        {
            entity.HasKey(e => e.LocalId).HasName("PK__POILocal__499359DBEF462064");

            entity.ToTable("POILocalizations");

            entity.Property(e => e.LocalId).HasColumnName("LocalID");
            entity.Property(e => e.AudioUrl).HasMaxLength(500);
            entity.Property(e => e.LanguageCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Poiid).HasColumnName("POIID");

            entity.HasOne(d => d.Poi).WithMany(p => p.Poilocalizations)
                .HasForeignKey(d => d.Poiid)
                .HasConstraintName("FK__POILocali__POIID__4222D4EF");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE3A343141C6");

            entity.ToTable("Role");

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(255);
        });

        modelBuilder.Entity<VisitLog>(entity =>
        {
            entity.HasKey(e => e.VisitId).HasName("PK__VisitLog__4D3AA1BEB4CD5DEA");

            entity.ToTable("VisitLog");

            entity.Property(e => e.VisitId).HasColumnName("VisitID");
            entity.Property(e => e.DeviceId)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("DeviceID");
            entity.Property(e => e.Poiid).HasColumnName("POIID");
            entity.Property(e => e.VisitTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Poi).WithMany(p => p.VisitLogs)
                .HasForeignKey(d => d.Poiid)
                .HasConstraintName("FK__VisitLog__POIID__5070F446");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
