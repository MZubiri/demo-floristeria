using System.Diagnostics;
using Floreria.API.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupController> _logger;

    public BackupController(IWebHostEnvironment env, IConfiguration configuration, ILogger<BackupController> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    private string BackupDirectory
    {
        get
        {
            var dir = Path.Combine(_env.ContentRootPath, "Backups");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }
    }

    [HttpGet("list")]
    public ActionResult<IEnumerable<BackupInfoDto>> ListBackups()
    {
        var dir = BackupDirectory;
        var files = Directory.GetFiles(dir, "*.sql")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupInfoDto(
                f.Name,
                f.Length,
                FormatFileSize(f.Length),
                f.CreationTimeUtc,
                $"/api/backup/download/{f.Name}"
            ))
            .ToList();

        return Ok(files);
    }

    [HttpPost("now")]
    public async Task<ActionResult<BackupInfoDto>> CreateBackupNow()
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"gestion_floreria_backup_{timestamp}.sql";
            var filePath = Path.Combine(BackupDirectory, fileName);

            var mysqldumpPath = FindMysqldumpBinary();
            if (string.IsNullOrEmpty(mysqldumpPath))
            {
                return StatusCode(500, new { message = "No se encontró el ejecutable mysqldump en el sistema." });
            }

            // Credenciales de conexión
            var connString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            string dbUser = "root";
            string dbPass = "mysql";
            string dbName = "gestion_floreria";

            var startInfo = new ProcessStartInfo
            {
                FileName = mysqldumpPath,
                Arguments = $"-u {dbUser} -p{dbPass} --single-transaction --quick --databases {dbName} -r \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !System.IO.File.Exists(filePath))
            {
                _logger.LogError("mysqldump falló con código {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return StatusCode(500, new { message = $"Error al generar respaldo: {stderr}" });
            }

            var fileInfo = new FileInfo(filePath);
            _logger.LogInformation("Respaldo MySQL generado con éxito: {File} ({Size} bytes)", fileName, fileInfo.Length);

            return Ok(new BackupInfoDto(
                fileName,
                fileInfo.Length,
                FormatFileSize(fileInfo.Length),
                fileInfo.CreationTimeUtc,
                $"/api/backup/download/{fileName}"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al generar respaldo de la base de datos");
            return StatusCode(500, new { message = $"Error interno: {ex.Message}" });
        }
    }

    [HttpGet("download/{fileName}")]
    public IActionResult DownloadBackup(string fileName)
    {
        // Sanitizar el nombre del archivo para prevenir path traversal
        var safeName = Path.GetFileName(fileName);
        var filePath = Path.Combine(BackupDirectory, safeName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "Archivo de respaldo no encontrado." });
        }

        var stream = System.IO.File.OpenRead(filePath);
        return File(stream, "application/sql", safeName);
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
            if (System.IO.File.Exists(path)) return path;
        }

        return "mysqldump";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
