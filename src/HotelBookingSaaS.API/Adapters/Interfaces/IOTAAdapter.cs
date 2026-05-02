using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;

namespace HotelBookingSaaS.API.Adapters.Interfaces;

/// <summary>
/// Contract for OTA adapters (Booking.com, Airbnb, Expedia via Channex).
/// Separate from IHotelPmsAdapter because OTA connections flow through Channex.
/// </summary>
public interface IOTAAdapter
{
    OTAType OTAType { get; }
    string OTAName { get; }

    void Initialize(OTACredential credential);

    Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default);

    Task<IEnumerable<StandardBookingDto>> GetBookingsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default);

    Task<bool> UpdateAvailabilityAsync(
        IEnumerable<StandardRoomAvailabilityDto> availability,
        CancellationToken ct = default);
}
