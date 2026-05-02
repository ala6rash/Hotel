using HotelBookingSaaS.API.Adapters.Implementations;
using HotelBookingSaaS.API.Adapters.Interfaces;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelBookingSaaS.API.Services;

public interface IPmsAdapterFactory
{
    IHotelPmsAdapter Create(PMSCredential credential);
    IHotelPmsAdapter Create(PmsType pmsType);
}

/// <summary>
/// Resolves the correct PMS adapter from DI based on PmsType.
/// Add new adapters here as new PMS integrations are built.
/// </summary>
public class PmsAdapterFactory : IPmsAdapterFactory
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PmsAdapterFactory> _logger;

    public PmsAdapterFactory(IServiceProvider services, ILogger<PmsAdapterFactory> logger)
    {
        _services = services;
        _logger = logger;
    }

    public IHotelPmsAdapter Create(PMSCredential credential)
    {
        var adapter = Create(credential.PmsType);
        adapter.Initialize(credential);
        return adapter;
    }

    public IHotelPmsAdapter Create(PmsType pmsType)
    {
        _logger.LogDebug("Creating PMS adapter for {PmsType}", pmsType);
        return pmsType switch
        {
            PmsType.SkyHorizon => _services.GetRequiredService<SkyHorizonAdapter>(),
            PmsType.OracleOpera => _services.GetRequiredService<OperaAdapter>(),
            PmsType.Mews => _services.GetRequiredService<MewsAdapter>(),
            _ => throw new NotSupportedException($"PMS type '{pmsType}' has no registered adapter.")
        };
    }
}
