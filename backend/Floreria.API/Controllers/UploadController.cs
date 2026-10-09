using Floreria.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IWebSyncService _webSyncService;
    private readonly ILogger<UploadController> _logger;

    public UploadController(
        IWebHostEnvironment env,
        IWebSyncService webSyncService,
        ILogger<UploadController> logger)
    {
        _env = env;
        _webSyncService = webSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Sube una fotografía directamente desde el dispositivo del usuario.
    /// Se aloja en el servidor de la tienda web para acceso público inmediato y se respalda localmente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No se ha seleccionado ningún archivo de imagen." });
        }

        // Máximo 10 MB
        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "El archivo excede el tamaño máximo permitido de 10 MB." });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Formato no permitido. Solo se aceptan imágenes JPG, JPEG, PNG y WEBP." });
        }

        try
        {
            var uniqueFileName = $"prod_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{extension}";

            // 1. Guardar copia local en wwwroot/uploads
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsDir = Path.Combine(webRoot, "uploads");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var localFilePath = Path.Combine(uploadsDir, uniqueFileName);
            using (var localStream = new FileStream(localFilePath, FileMode.Create))
            {
                await file.CopyToAsync(localStream);
            }

            // 2. Subir a la tienda web en vivo para disponibilidad en la nube pública
            string? webUrl = null;
            using (var memStream = file.OpenReadStream())
            {
                webUrl = await _webSyncService.UploadImageToWebAsync(memStream, uniqueFileName, file.ContentType ?? "image/jpeg");
            }

            var finalUrl = !string.IsNullOrEmpty(webUrl)
                ? webUrl
                : $"{Request.Scheme}://{Request.Host}/uploads/{uniqueFileName}";

            _logger.LogInformation("Fotografía procesada exitosamente: {Url}", finalUrl);

            return Ok(new
            {
                url = finalUrl,
                fileName = uniqueFileName,
                message = "Fotografía subida y alojada correctamente."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar la subida de fotografía");
            return StatusCode(500, new { message = "Error interno al guardar la fotografía: " + ex.Message });
        }
    }
}
