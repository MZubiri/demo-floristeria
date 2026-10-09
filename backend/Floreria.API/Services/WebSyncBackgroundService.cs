using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Floreria.API.Services;

/// <summary>
/// Servicio en segundo plano que mantiene el ERP y la Tienda Web permanentemente sincronizados.
/// Consulta pedidos nuevos en la web cada 15 segundos y sincroniza los cambios de inventario y catálogo.
/// </summary>
public class WebSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebSyncBackgroundService> _logger;

    public WebSyncBackgroundService(IServiceProvider serviceProvider, ILogger<WebSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebSyncBackgroundService iniciado: Sincronización continua activa cada 15 segundos.");

        // Pequeña pausa inicial de 4 segundos para esperar a que los servicios y MySQL estén listos
        await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IWebSyncService>();
                var result = await syncService.SyncAllAsync();
                if (result.Success && result.OrdersSynced > 0)
                {
                    _logger.LogInformation("Sincronización continua: {Msg}", result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Ciclo de sincronización continua: {Msg}", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("WebSyncBackgroundService detenido.");
    }
}
