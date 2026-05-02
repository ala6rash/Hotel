using FluentAssertions;
using HotelBookingSaaS.API.Adapters.Implementations;
using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Hubs;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotelBookingSaaS.Tests.ServiceTests;

public class BookingSyncServiceTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IPmsAdapterFactory> _pmsFactoryMock = new();
    private readonly Mock<ChannexAdapter> _channexMock;
    private readonly Mock<IHubContext<BookingHub>> _hubMock = new();
    private readonly BookingSyncService _sut;

    public BookingSyncServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        // Mock SignalR hub
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        _hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _channexMock = new Mock<ChannexAdapter>(
            new Mock<IHttpClientFactory>().Object,
            new Mock<IEncryptionService>().Object,
            NullLogger<ChannexAdapter>.Instance);

        _sut = new BookingSyncService(
            _db,
            _pmsFactoryMock.Object,
            _channexMock.Object,
            _hubMock.Object,
            NullLogger<BookingSyncService>.Instance);
    }

    [Fact]
    public async Task SyncFromWebhook_NewBooking_IsPersistedToDatabase()
    {
        await SeedHotelAsync(1);

        var booking = new StandardBookingDto
        {
            ExternalBookingId = "BOOK-001",
            OTAType = OTAType.BookingCom,
            Source = BookingSource.Webhook,
            GuestName = "Mohammed Al-Khalidi",
            GuestEmail = "m.khalidi@test.com",
            CheckInDate = DateTime.Parse("2025-07-01"),
            CheckOutDate = DateTime.Parse("2025-07-05"),
            TotalPrice = 320m,
            Currency = "JOD",
            Status = BookingStatus.Confirmed
        };

        await _sut.SyncFromWebhookAsync(1, booking);

        var saved = await _db.Bookings.FirstOrDefaultAsync(b => b.OTABookingId == "BOOK-001");
        saved.Should().NotBeNull();
        saved!.GuestName.Should().Be("Mohammed Al-Khalidi");
        saved.TotalPrice.Should().Be(320m);
    }

    [Fact]
    public async Task SyncFromWebhook_DuplicateBooking_UpdatesExisting()
    {
        await SeedHotelAsync(1);

        var booking = new StandardBookingDto
        {
            ExternalBookingId = "BOOK-DUP",
            OTAType = OTAType.Airbnb,
            Source = BookingSource.Webhook,
            GuestName = "Sara Al-Ahmad",
            CheckInDate = DateTime.Parse("2025-08-01"),
            CheckOutDate = DateTime.Parse("2025-08-03"),
            TotalPrice = 150m,
            Currency = "JOD",
            Status = BookingStatus.Confirmed
        };

        // First call — creates
        await _sut.SyncFromWebhookAsync(1, booking);

        // Second call with updated status — should update, not duplicate
        booking = booking with { Status = BookingStatus.Cancelled, TotalPrice = 0m };
        await _sut.SyncFromWebhookAsync(1, booking);

        var bookings = await _db.Bookings
            .Where(b => b.OTABookingId == "BOOK-DUP")
            .ToListAsync();

        bookings.Should().HaveCount(1); // No duplicate
        bookings[0].BookingStatus.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task SyncFromWebhook_NewBooking_CreatesNotification()
    {
        await SeedHotelAsync(1);

        var booking = new StandardBookingDto
        {
            ExternalBookingId = "BOOK-NOTIF",
            OTAType = OTAType.Expedia,
            GuestName = "Layla Hassan",
            CheckInDate = DateTime.Parse("2025-09-01"),
            CheckOutDate = DateTime.Parse("2025-09-04"),
            Status = BookingStatus.Confirmed
        };

        await _sut.SyncFromWebhookAsync(1, booking);

        var notification = await _db.Notifications.FirstOrDefaultAsync(n =>
            n.HotelId == 1 && n.Type == NotificationType.NewBooking);

        notification.Should().NotBeNull();
        notification!.Message.Should().Contain("Layla Hassan");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task SeedHotelAsync(int hotelId)
    {
        _db.Hotels.Add(new Hotel
        {
            HotelId = hotelId,
            HotelName = "Test Hotel",
            City = "Amman",
            Country = "Jordan",
            PmsType = PmsType.Manual,
            IsActive = true
        });
        await _db.SaveChangesAsync();
    }
}
