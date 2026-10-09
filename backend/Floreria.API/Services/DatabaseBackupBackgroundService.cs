using System.Diagnostics;

namespace Floreria.API.Services;

public class DatabaseBackupBackgroundService : BackgroundService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupBackgroundService> _logger;

    public DatabaseBackupBackgroundService(
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<DatabaseBackupBackgroundService> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de respaldo automático de Base de Datos iniciado.");

        // Esperar 1 minuto tras iniciar para no sobrecargar el arranque
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunDailyBackup();
                PurgeOldBackups();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en el ciclo de respaldo automático de Base de Datos");
            }

            // Repetir cada 12 horas
            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }

    private void RunDailyBackup()
    {
        var dir = Path.Combine(_env.ContentRootPath, "Backups");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        // Verificar si ya existe un respaldo para hoy
        var existingToday = Directory.GetFiles(dir, $"floreria_lacarreta_backup_{today}_*.sql");
        if (existingToday.Any())
        {
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"floreria_lacarreta_backup_{timestamp}.sql";
        var filePath = Path.Combine(dir, fileName);

        var mysqldumpPath = FindMysqldumpBinary();
        if (string.IsNullOrEmpty(mysqldumpPath)) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = mysqldumpPath,
            Arguments = $"-u root -pmysql --single-transaction --quick --databases floreria_lacarreta_db -r \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit(30000);

        if (process.ExitCode == 0 && File.Exists(filePath))
        {
            _logger.LogInformation("Respaldo diario automático creado: {File}", fileName);
        }
    }

    private void PurgeOldBackups()
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, "Backups");
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.sql")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(20) // Mantener los 20 más recientes
                .ToList();

            foreach (var f in files)
            {
                f.Delete();
            }
        }
        catch { }
    }

    private static string? FindMysqldumpBinary()
    {
        var candidates = new[]
        {
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.1\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Workbench 8.0\mysqldump.exe",
            @"C:\laragon\bin\mysql\mysql-8.0.30-winx64\bin\mysqldump.exe",
            @"C:\xampp\mysql\bin\mysqldump.exe"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        return "mysqldump";
    }
}
