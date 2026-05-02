using FluentAssertions;
using HotelBookingSaaS.API.Adapters.Implementations;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HotelBookingSaaS.Tests.AdapterTests;

public class SkyHorizonAdapterTests
{
    private readonly Mock<IHttpClientFactory> _clientFactoryMock = new();
    private readonly EncryptionService _encryption;

    public SkyHorizonAdapterTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "TestEncryptionKey_ForUnitTests_Only_32"
            })
            .Build();
        _encryption = new EncryptionService(config);
    }

    [Fact]
    public void PmsType_IsSkyHorizon()
    {
        var adapter = CreateAdapter(CreateMockHttpClient("{\"status\":\"ok\",\"version\":\"2.0\"}"));
        adapter.PmsType.Should().Be(PmsType.SkyHorizon);
    }

    [Fact]
    public async Task TestConnection_WhenApiReturnsOk_ReturnsConnected()
    {
        var json = JsonSerializer.Serialize(new { status = "ok", version = "2.1" });
        var adapter = CreateAdapter(CreateMockHttpClient(json));
        adapter.Initialize(BuildCredential());

        var result = await adapter.TestConnectionAsync();

        result.IsConnected.Should().BeTrue();
        result.Message.Should().Contain("Sky Horizon");
    }

    [Fact]
    public async Task TestConnection_WhenApiFails_ReturnsNotConnected()
    {
        var adapter = CreateAdapter(CreateMockHttpClient("Error", HttpStatusCode.InternalServerError));
        adapter.Initialize(BuildCredential());

        var result = await adapter.TestConnectionAsync();

        result.IsConnected.Should().BeFalse();
        result.Message.Should().Contain("failed");
    }

    [Fact]
    public async Task GetBookings_MapsGuestNameCorrectly()
    {
        var reservations = new
        {
            reservations = new[]
            {
                new
                {
                    reservationId = "RES001",
                    confirmationNumber = "CONF001",
                    guestFirstName = "Ahmad",
                    guestLastName = "Al-Rashid",
                    guestEmail = "ahmad@test.com",
                    guestPhone = "+962799000000",
                    guestNationality = "JO",
                    arrivalDate = "2025-06-01",
                    departureDate = "2025-06-05",
                    adults = 2,
                    children = 0,
                    roomType = "Double",
                    roomNumber = "101",
                    totalAmount = 250.00m,
                    currency = "JOD",
                    ratePlan = "BAR",
                    status = "confirmed",
                    specialRequests = "Late check-in",
                    createdAt = "2025-05-01T10:00:00Z"
                }
            },
            total = 1
        };

        var json = JsonSerializer.Serialize(reservations);
        var adapter = CreateAdapter(CreateMockHttpClient(json));
        adapter.Initialize(BuildCredential());

        var bookings = (await adapter.GetBookingsAsync(
            DateTime.Parse("2025-06-01"),
            DateTime.Parse("2025-06-30"))).ToList();

        bookings.Should().HaveCount(1);
        bookings[0].GuestName.Should().Be("Ahmad Al-Rashid");
        bookings[0].CheckInDate.Should().Be(DateTime.Parse("2025-06-01"));
        bookings[0].Status.Should().Be(BookingStatus.Confirmed);
        bookings[0].TotalPrice.Should().Be(250.00m);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private SkyHorizonAdapter CreateAdapter(HttpClient client)
    {
        _clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return new SkyHorizonAdapter(
            _clientFactoryMock.Object,
            _encryption,
            NullLogger<SkyHorizonAdapter>.Instance);
    }

    private static HttpClient CreateMockHttpClient(string responseJson,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.skyhorizon.test/v2/")
        };
    }

    private PMSCredential BuildCredential() => new()
    {
        CredentialId = 1,
        HotelId = 1,
        PmsType = PmsType.SkyHorizon,
        ApiUrl = "https://api.skyhorizon.test/v2/",
        ApiKeyEncrypted = _encryption.Encrypt("test-api-key"),
        IsActive = true
    };
}
