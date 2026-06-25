using CourierMax.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourierMax.Infrastructure.Persistence;

public sealed class CourierMaxDbContext : DbContext
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<CityDistance> CityDistances => Set<CityDistance>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    public CourierMaxDbContext(DbContextOptions<CourierMaxDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<City>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<CityDistance>(b =>
        {
            b.HasKey(cd => cd.Id);
            b.Property(cd => cd.DistanceKm).HasColumnType("decimal(10,2)");
            b.Property(cd => cd.DistanceFee).HasColumnType("decimal(10,2)");
            b.HasIndex(cd => new { cd.OriginCityId, cd.DestinationCityId }).IsUnique();
        });

        modelBuilder.Entity<PublicHoliday>(b =>
        {
            b.HasKey(h => h.Id);
            b.HasIndex(h => h.Date).IsUnique();
        });

        modelBuilder.Entity<Vehicle>(b =>
        {
            b.HasKey(v => v.Id);
            b.Property(v => v.Plate).IsRequired().HasMaxLength(20);
            b.HasIndex(v => v.Plate).IsUnique();
            b.Property(v => v.MaxWeightKg).HasColumnType("decimal(10,2)");
            b.Property(v => v.MaxVolumeM3).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<Driver>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(150);
            b.HasOne(d => d.Vehicle).WithMany().HasForeignKey(d => d.VehicleId);
        });

        modelBuilder.Entity<Shipment>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.TrackingCode).IsRequired().HasMaxLength(20);
            b.HasIndex(s => s.TrackingCode).IsUnique();

            // Sender (ContactInfo) — Owned Type, mapeado como columnas con prefijo.
            b.OwnsOne(s => s.Sender, sa =>
            {
                sa.Property(c => c.Name).HasColumnName("SenderName").IsRequired().HasMaxLength(150);
                sa.Property(c => c.Phone).HasColumnName("SenderPhone").IsRequired().HasMaxLength(10);
                sa.Property(c => c.Address).HasColumnName("SenderAddress").IsRequired().HasMaxLength(300);
            });

            // Recipient (ContactInfo) — Owned Type.
            b.OwnsOne(s => s.Recipient, ra =>
            {
                ra.Property(c => c.Name).HasColumnName("RecipientName").IsRequired().HasMaxLength(150);
                ra.Property(c => c.Phone).HasColumnName("RecipientPhone").IsRequired().HasMaxLength(10);
                ra.Property(c => c.Address).HasColumnName("RecipientAddress").IsRequired().HasMaxLength(300);
            });

            // Package (PackageInfo) — Owned Type.
            b.OwnsOne(s => s.Package, p =>
            {
                p.Property(pk => pk.WeightKg).HasColumnName("PackageWeightKg").HasColumnType("decimal(6,2)");
                p.Property(pk => pk.LengthCm).HasColumnName("PackageLengthCm").HasColumnType("decimal(6,2)");
                p.Property(pk => pk.WidthCm).HasColumnName("PackageWidthCm").HasColumnType("decimal(6,2)");
                p.Property(pk => pk.HeightCm).HasColumnName("PackageHeightCm").HasColumnType("decimal(6,2)");
                p.Property(pk => pk.Type).HasColumnName("PackageType");
            });

            b.Property(s => s.Cost).HasColumnType("decimal(10,2)");
            b.Property(s => s.ServiceType);
            b.Property(s => s.Status);

            // StatusHistory: relación 1-N con StatusChange, expuesta como campo
            // privado (_statusHistory) mapeado por EF Core a la colección de
            // navegación. Se usa backing field para preservar el encapsulamiento
            // del agregado (no exponer un setter público de la lista).
            // EF Core necesita acceso al backing field "_statusHistory" porque
            // StatusHistory solo expone un getter (encapsulamiento del agregado).
            b.HasMany(s => s.StatusHistory)
                .WithOne()
                .HasForeignKey(sc => sc.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation(s => s.StatusHistory)
                .UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
        });

        modelBuilder.Entity<StatusChange>(b =>
        {
            b.HasKey(sc => sc.Id);
            b.Property(sc => sc.Reason).HasMaxLength(500);
            b.Property(sc => sc.ChangedByUserId).IsRequired().HasMaxLength(100);
        });
    }
}
