using HotelBookingSaaS.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSaaS.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<OTACredential> OTACredentials => Set<OTACredential>();
    public DbSet<PMSCredential> PMSCredentials => Set<PMSCredential>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Hotel
        modelBuilder.Entity<Hotel>(e =>
        {
            e.HasIndex(h => h.HotelName);
            e.HasIndex(h => h.IsActive);
        });

        // Room — unique room number per hotel
        modelBuilder.Entity<Room>(e =>
        {
            e.HasIndex(r => new { r.HotelId, r.RoomNumber }).IsUnique();
            e.HasOne(r => r.Hotel)
                .WithMany(h => h.Rooms)
                .HasForeignKey(r => r.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Booking — unique OTA booking ID per hotel+OTA combination
        modelBuilder.Entity<Booking>(e =>
        {
            // No HasFilter — SQLite doesn't support filtered indexes
            e.HasIndex(b => new { b.HotelId, b.OTAType, b.OTABookingId })
                .IsUnique();

            e.HasIndex(b => b.CheckInDate);
            e.HasIndex(b => b.CheckOutDate);
            e.HasIndex(b => b.BookingStatus);
            e.HasIndex(b => b.HotelId);

            e.HasOne(b => b.Hotel)
                .WithMany(h => h.Bookings)
                .HasForeignKey(b => b.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OTACredential — one credential per hotel+OTA
        modelBuilder.Entity<OTACredential>(e =>
        {
            e.HasIndex(c => new { c.HotelId, c.OTAType }).IsUnique();
            e.HasOne(c => c.Hotel)
                .WithMany(h => h.OTACredentials)
                .HasForeignKey(c => c.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PMSCredential — one per hotel+PMS type
        modelBuilder.Entity<PMSCredential>(e =>
        {
            e.HasIndex(c => new { c.HotelId, c.PmsType }).IsUnique();
            e.HasOne(c => c.Hotel)
                .WithMany(h => h.PMSCredentials)
                .HasForeignKey(c => c.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SyncLog
        modelBuilder.Entity<SyncLog>(e =>
        {
            e.HasIndex(s => s.HotelId);
            e.HasIndex(s => s.SyncStartTime);
            e.HasIndex(s => s.Status);
            e.HasOne(s => s.Hotel)
                .WithMany(h => h.SyncLogs)
                .HasForeignKey(s => s.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Hotel)
                .WithMany(h => h.Users)
                .HasForeignKey(u => u.HotelId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Notification
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.HotelId, n.IsRead });
            e.HasIndex(n => n.CreatedDate);
            e.HasOne(n => n.Hotel)
                .WithMany(h => h.Notifications)
                .HasForeignKey(n => n.HotelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial SuperAdmin user (password: Admin@123 — change in production!)
        modelBuilder.Entity<User>().HasData(new User
        {
            UserId = 1,
            HotelId = null,
            Email = "admin@hotelsaas.jo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            FirstName = "Super",
            LastName = "Admin",
            Role = Enums.UserRole.SuperAdmin,
            IsActive = true,
            CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    /// <summary>
    /// Automatically updates UpdatedDate on every save.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is Hotel hotel)
                hotel.UpdatedDate = DateTime.UtcNow;
            else if (entry.Entity is OTACredential otaCred)
                otaCred.UpdatedDate = DateTime.UtcNow;
            else if (entry.Entity is PMSCredential pmsCred)
                pmsCred.UpdatedDate = DateTime.UtcNow;
            else if (entry.Entity is Booking booking)
                booking.UpdatedDate = DateTime.UtcNow;
            else if (entry.Entity is User user)
                user.UpdatedDate = DateTime.UtcNow;
        }
    }
}
