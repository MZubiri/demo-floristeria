using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly FloreriaDbContext _context;

    public AttendanceController(FloreriaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetByDate([FromQuery] string? date)
    {
        var targetDate = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : date;

        // Asegurar que todos los usuarios activos tengan un registro inicial o aparezcan en la lista para pasar lista
        var users = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive)
            .ToListAsync();

        var existingRecords = await _context.Attendances
            .Include(a => a.User)
                .ThenInclude(u => u!.Role)
            .Where(a => a.Date == targetDate)
            .ToListAsync();

        var result = new List<AttendanceDto>();

        foreach (var user in users)
        {
            var record = existingRecords.FirstOrDefault(r => r.UserId == user.Id);
            if (record != null)
            {
                result.Add(new AttendanceDto(
                    record.Id,
                    user.Id,
                    user.Name,
                    user.Role?.Name ?? "Colaborador",
                    record.Date,
                    record.ClockIn,
                    record.ClockOut,
                    record.Status,
                    record.Notes
                ));
            }
            else
            {
                // Todavía no tiene registro hoy, se muestra como pendiente
                result.Add(new AttendanceDto(
                    0,
                    user.Id,
                    user.Name,
                    user.Role?.Name ?? "Colaborador",
                    targetDate,
                    null,
                    null,
                    "Sin registrar",
                    null
                ));
            }
        }

        return Ok(result.OrderBy(r => r.UserName));
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetHistory(
        [FromQuery] int? userId,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var query = _context.Attendances
            .Include(a => a.User)
                .ThenInclude(u => u!.Role)
            .AsQueryable();

        if (userId.HasValue && userId.Value > 0)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(from))
        {
            query = query.Where(a => a.Date.CompareTo(from) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            query = query.Where(a => a.Date.CompareTo(to) <= 0);
        }

        var records = await query
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.User != null ? a.User.Name : "")
            .Select(a => new AttendanceDto(
                a.Id,
                a.UserId,
                a.User != null ? a.User.Name : "Desconocido",
                a.User != null && a.User.Role != null ? a.User.Role.Name : "Colaborador",
                a.Date,
                a.ClockIn,
                a.ClockOut,
                a.Status,
                a.Notes
            ))
            .ToListAsync();

        return Ok(records);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AttendanceSummaryDto>> GetSummary([FromQuery] string? date)
    {
        var targetDate = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : date;
        var totalActive = await _context.Users.CountAsync(u => u.IsActive);
        var records = await _context.Attendances.Where(a => a.Date == targetDate).ToListAsync();

        var present = records.Count(r => r.Status == "Presente");
        var late = records.Count(r => r.Status == "Retardo");
        var absent = records.Count(r => r.Status == "Falta");
        var justified = records.Count(r => r.Status == "Justificado" || r.Status == "Permiso");

        return Ok(new AttendanceSummaryDto(totalActive, present, late, absent, justified));
    }

    [HttpPost("mark")]
    public async Task<ActionResult<AttendanceDto>> MarkAttendance([FromBody] MarkAttendanceDto dto)
    {
        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == dto.UserId);
        if (user == null) return NotFound(new { message = "Trabajador no encontrado." });

        var targetDate = string.IsNullOrWhiteSpace(dto.Date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : dto.Date;
        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.Date == targetDate);

        if (record == null)
        {
            record = new Attendance
            {
                UserId = dto.UserId,
                Date = targetDate,
                ClockIn = dto.ClockIn ?? DateTime.Now.ToString("HH:mm"),
                ClockOut = dto.ClockOut,
                Status = dto.Status,
                Notes = dto.Notes
            };
            _context.Attendances.Add(record);
        }
        else
        {
            record.Status = dto.Status;
            if (!string.IsNullOrWhiteSpace(dto.ClockIn)) record.ClockIn = dto.ClockIn;
            if (!string.IsNullOrWhiteSpace(dto.ClockOut)) record.ClockOut = dto.ClockOut;
            if (dto.Notes != null) record.Notes = dto.Notes;
        }

        await _context.SaveChangesAsync();

        return Ok(new AttendanceDto(
            record.Id,
            user.Id,
            user.Name,
            user.Role?.Name ?? "Colaborador",
            record.Date,
            record.ClockIn,
            record.ClockOut,
            record.Status,
            record.Notes
        ));
    }

    [HttpPost("clock-in")]
    public async Task<ActionResult<AttendanceDto>> ClockIn([FromBody] ClockInOutDto dto)
    {
        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == dto.UserId);
        if (user == null) return NotFound(new { message = "Trabajador no encontrado." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var nowTime = DateTime.Now.ToString("HH:mm");

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.Date == today);
        if (record == null)
        {
            record = new Attendance
            {
                UserId = dto.UserId,
                Date = today,
                ClockIn = nowTime,
                Status = "Presente",
                Notes = dto.Notes ?? "Entrada registrada por el colaborador"
            };
            _context.Attendances.Add(record);
        }
        else
        {
            record.ClockIn = nowTime;
            if (dto.Notes != null) record.Notes = dto.Notes;
        }

        await _context.SaveChangesAsync();

        return Ok(new AttendanceDto(
            record.Id,
            user.Id,
            user.Name,
            user.Role?.Name ?? "Colaborador",
            record.Date,
            record.ClockIn,
            record.ClockOut,
            record.Status,
            record.Notes
        ));
    }

    [HttpPost("clock-out")]
    public async Task<ActionResult<AttendanceDto>> ClockOut([FromBody] ClockInOutDto dto)
    {
        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == dto.UserId);
        if (user == null) return NotFound(new { message = "Trabajador no encontrado." });

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var nowTime = DateTime.Now.ToString("HH:mm");

        var record = await _context.Attendances.FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.Date == today);
        if (record == null)
        {
            record = new Attendance
            {
                UserId = dto.UserId,
                Date = today,
                ClockIn = "08:00",
                ClockOut = nowTime,
                Status = "Presente",
                Notes = dto.Notes ?? "Salida registrada"
            };
            _context.Attendances.Add(record);
        }
        else
        {
            record.ClockOut = nowTime;
            if (dto.Notes != null) record.Notes = dto.Notes;
        }

        await _context.SaveChangesAsync();

        return Ok(new AttendanceDto(
            record.Id,
            user.Id,
            user.Name,
            user.Role?.Name ?? "Colaborador",
            record.Date,
            record.ClockIn,
            record.ClockOut,
            record.Status,
            record.Notes
        ));
    }
}
