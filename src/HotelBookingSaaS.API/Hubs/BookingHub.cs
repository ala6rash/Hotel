using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HotelBookingSaaS.API.Hubs;

/// <summary>
/// SignalR hub for real-time booking notifications.
/// Clients join a group per hotel: "hotel_{hotelId}".
/// </summary>
[Authorize]
public class BookingHub : Hub
{
    private readonly ILogger<BookingHub> _logger;

    public BookingHub(ILogger<BookingHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinHotelGroup(int hotelId)
    {
        // Validate the user belongs to this hotel (claim check)
        var userHotelId = Context.User?.FindFirst("hotelId")?.Value;
        var isAdmin = Context.User?.IsInRole("SuperAdmin") ?? false;

        if (!isAdmin && userHotelId != hotelId.ToString())
        {
            _logger.LogWarning("User {User} tried to join unauthorized hotel group {HotelId}",
                Context.UserIdentifier, hotelId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"hotel_{hotelId}");
        _logger.LogDebug("Connection {ConnId} joined hotel group {HotelId}",
            Context.ConnectionId, hotelId);
    }

    public async Task LeaveHotelGroup(int hotelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hotel_{hotelId}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected: {ConnId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
