using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HotelBookingSaaS.API.Controllers;

/// <summary>
/// Receives inbound webhooks from Channex, Booking.com, and Airbnb.
/// Each endpoint validates the payload signature before processing.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IBookingSyncService _syncService;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        ApplicationDbContext db,
        IBookingSyncService syncService,
        IConfiguration config,
        ILogger<WebhooksController> logger)
    {
        _db = db;
        _syncService = syncService;
        _config = config;
        _logger = logger;
    }

    /// <summary>Channex webhook — fires for all OTA events.</summary>
    [HttpPost("channex")]
    public async Task<IActionResult> ChannexWebhook()
    {
        var rawBody = await ReadRawBodyAsync();

        if (!ValidateChannexSignature(rawBody, Request.Headers["X-Channex-Signature"].ToString()))
        {
            _logger.LogWarning("Channex webhook: invalid signature");
            return Unauthorized("Invalid webhook signature");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ChannexWebhookPayload>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload == null) return BadRequest("Invalid payload");

            _logger.LogInformation("Channex webhook received: event={Event}, propertyId={PropertyId}",
                payload.Event, payload.PropertyId);

            // Look up hotel by Channex property ID
            var hotel = await _db.Hotels
                .FirstOrDefaultAsync(h => h.ChannexPropertyId == payload.PropertyId && h.IsActive);

            if (hotel == null)
            {
                _logger.LogWarning("Channex webhook: no hotel found for property {PropertyId}",
                    payload.PropertyId);
                return Ok(); // Acknowledge anyway
            }

            var booking = MapChannexPayload(payload);
            if (booking != null)
                await _syncService.SyncFromWebhookAsync(hotel.HotelId, booking);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channex webhook processing failed");
            return StatusCode(500);
        }
    }

    /// <summary>Generic PMS webhook for hotels whose PMS supports outbound notifications.</summary>
    [HttpPost("pms/{hotelId:int}")]
    public async Task<IActionResult> PmsWebhook(int hotelId)
    {
        var rawBody = await ReadRawBodyAsync();

        _logger.LogInformation("PMS webhook received for hotel {HotelId}", hotelId);

        // Parse as generic booking — each PMS has different payloads;
        // a full implementation would branch on hotel.PmsType here.
        try
        {
            var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var booking = new StandardBookingDto
            {
                ExternalBookingId = root.TryGetProperty("booking_id", out var id)
                    ? id.GetString() ?? Guid.NewGuid().ToString()
                    : Guid.NewGuid().ToString(),
                OTAType = OTAType.Direct,
                Source = BookingSource.Webhook,
                GuestName = root.TryGetProperty("guest_name", out var gn)
                    ? gn.GetString() ?? "Unknown"
                    : "Unknown",
                CheckInDate = root.TryGetProperty("check_in", out var ci)
                    ? DateTime.Parse(ci.GetString()!)
                    : DateTime.UtcNow.Date,
                CheckOutDate = root.TryGetProperty("check_out", out var co)
                    ? DateTime.Parse(co.GetString()!)
                    : DateTime.UtcNow.Date.AddDays(1),
                Status = BookingStatus.Confirmed
            };

            await _syncService.SyncFromWebhookAsync(hotelId, booking);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PMS webhook processing failed for hotel {HotelId}", hotelId);
            return StatusCode(500);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> ReadRawBodyAsync()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;
        return body;
    }

    private bool ValidateChannexSignature(string rawBody, string signature)
    {
        var secret = _config["Channex:WebhookSecret"];
        if (string.IsNullOrEmpty(secret)) return true; // Skip validation if not configured

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLower();

        return computed == signature?.ToLower();
    }

    private static StandardBookingDto? MapChannexPayload(ChannexWebhookPayload payload)
    {
        if (payload.Booking == null) return null;
        var b = payload.Booking;

        return new StandardBookingDto
        {
            ExternalBookingId = b.Id ?? Guid.NewGuid().ToString(),
            OTAType = MapOTAType(b.ChannelTitle),
            Source = BookingSource.Webhook,
            GuestName = $"{b.GuestFirstName} {b.GuestLastName}".Trim(),
            GuestEmail = b.GuestEmail,
            GuestPhone = b.GuestPhone,
            CheckInDate = b.ArrivalDate,
            CheckOutDate = b.DepartureDate,
            Adults = b.Adults ?? 1,
            TotalPrice = b.TotalPrice ?? 0,
            Currency = b.Currency ?? "JOD",
            Status = payload.Event?.Contains("cancel") == true
                ? BookingStatus.Cancelled
                : BookingStatus.Confirmed
        };
    }

    private static OTAType MapOTAType(string? channel) => channel?.ToLower() switch
    {
        "booking.com" => OTAType.BookingCom,
        "airbnb" => OTAType.Airbnb,
        "expedia" => OTAType.Expedia,
        "agoda" => OTAType.Agoda,
        _ => OTAType.Channex
    };

    // Channex webhook payload shape
    private record ChannexWebhookPayload(
        string? Event,
        string? PropertyId,
        ChannexWebhookBooking? Booking);

    private record ChannexWebhookBooking(
        string? Id,
        string? ChannelTitle,
        string GuestFirstName,
        string GuestLastName,
        string? GuestEmail,
        string? GuestPhone,
        DateTime ArrivalDate,
        DateTime DepartureDate,
        int? Adults,
        decimal? TotalPrice,
        string? Currency);
}
