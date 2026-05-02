using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;

namespace HotelBookingSaaS.API.Adapters.Interfaces;

/// <summary>
/// Contract that every PMS adapter must fulfill.
/// Each PMS (Sky Horizon, Opera, Mews, etc.) has its own implementation
/// but all expose the same surface to the sync engine.
/// </summary>
public interface IHotelPmsAdapter
{
    /// <summary>Identifies which PMS this adapter handles.</summary>
    PmsType PmsType { get; }

    /// <summary>Human-readable PMS name for logging/UI.</summary>
    string PmsName { get; }

    /// <summary>
    /// Initialise the adapter with credentials for a specific hotel.
    /// Call this before any data operations.
    /// </summary>
    void Initialize(PMSCredential credential);

    /// <summary>
    /// Verify the credentials are valid and the PMS is reachable.
    /// Returns detailed result for UI feedback.
    /// </summary>
    Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Pull all bookings in the given date window.
    /// Returns a flat list in the StandardBookingDto format —
    /// all PMS-specific fields are normalised here.
    /// </summary>
    Task<IEnumerable<StandardBookingDto>> GetBookingsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Pull real-time availability for one or more room types.
    /// </summary>
    Task<IEnumerable<StandardRoomAvailabilityDto>> GetAvailabilityAsync(
        DateTime fromDate,
        DateTime toDate,
        IEnumerable<string>? roomTypeIds = null,
        CancellationToken ct = default);

    /// <summary>
    /// Push a new booking into the PMS (used when a booking arrives from an OTA
    /// and needs to be reflected in the hotel's own system).
    /// Returns the PMS confirmation number.
    /// </summary>
    Task<string?> CreateBookingAsync(StandardBookingDto booking, CancellationToken ct = default);

    /// <summary>
    /// Update an existing booking in the PMS (e.g. status change, modification).
    /// </summary>
    Task<bool> UpdateBookingAsync(string pmsBookingId, StandardBookingDto booking, CancellationToken ct = default);

    /// <summary>
    /// Cancel a booking in the PMS.
    /// </summary>
    Task<bool> CancelBookingAsync(string pmsBookingId, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Refresh the OAuth / session token if it is about to expire.
    /// Returns the updated credential so it can be persisted.
    /// </summary>
    Task<PMSCredential?> RefreshTokenIfNeededAsync(PMSCredential credential, CancellationToken ct = default);
}
