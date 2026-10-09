using Floreria.API.Data;
using Floreria.API.DTOs;
using Floreria.API.Models;
using Floreria.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Floreria.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly FloreriaDbContext _context;
    private readonly IAuthService _authService;

    public AuthController(FloreriaDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower());

        if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Credenciales incorrectas o usuario no encontrado." });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "El usuario se encuentra inactivo. Contacta a un administrador." });
        }

        var token = _authService.GenerateJwtToken(user);
        var userDto = new UserDto(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.RoleId,
            user.Role?.Name ?? "Colaborador",
            user.IsActive,
            user.CreatedAt,
            ParsePermissions(user.Role?.PermissionsJson)
        );

        return Ok(new LoginResponse(token, userDto));
    }

    public static List<string> ParsePermissions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync();

        if (user == null) return NotFound();

        return Ok(new UserDto(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.RoleId,
            user.Role?.Name ?? "Colaborador",
            user.IsActive,
            user.CreatedAt,
            ParsePermissions(user.Role?.PermissionsJson)
        ));
    }
}

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly FloreriaDbContext _context;
    private readonly IAuthService _authService;

    public UsersController(FloreriaDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .OrderBy(u => u.Name)
            .ToListAsync();

        var result = users.Select(u => new UserDto(
            u.Id,
            u.Name,
            u.Email,
            u.Phone,
            u.RoleId,
            u.Role != null ? u.Role.Name : "Sin rol",
            u.IsActive,
            u.CreatedAt,
            AuthController.ParsePermissions(u.Role?.PermissionsJson)
        )).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var u = await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (u == null) return NotFound(new { message = "Usuario no encontrado." });

        return Ok(new UserDto(
            u.Id,
            u.Name,
            u.Email,
            u.Phone,
            u.RoleId,
            u.Role?.Name ?? "Sin rol",
            u.IsActive,
            u.CreatedAt,
            AuthController.ParsePermissions(u.Role?.PermissionsJson)
        ));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email.Trim().ToLower()))
        {
            return BadRequest(new { message = "Ya existe un usuario con este correo electrónico." });
        }

        var role = await _context.Roles.FindAsync(dto.RoleId);
        if (role == null)
        {
            return BadRequest(new { message = "El rol seleccionado no existe." });
        }

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim().ToLower(),
            PasswordHash = _authService.HashPassword(dto.Password),
            Phone = dto.Phone?.Trim() ?? "",
            RoleId = dto.RoleId,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new UserDto(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.RoleId,
            role.Name,
            user.IsActive,
            user.CreatedAt
        ));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Usuario no encontrado." });

        var emailExists = await _context.Users.AnyAsync(u => u.Id != id && u.Email == dto.Email.Trim().ToLower());
        if (emailExists) return BadRequest(new { message = "El correo ya está en uso por otro colaborador." });

        user.Name = dto.Name.Trim();
        user.Email = dto.Email.Trim().ToLower();
        user.Phone = dto.Phone?.Trim() ?? "";
        user.RoleId = dto.RoleId;
        user.IsActive = dto.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Usuario no encontrado." });

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "La nueva contraseña debe tener al menos 6 caracteres." });
        }

        user.PasswordHash = _authService.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Contraseña actualizada exitosamente." });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Usuario no encontrado." });

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { isActive = user.IsActive });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Usuario no encontrado." });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly FloreriaDbContext _context;

    public RolesController(FloreriaDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll()
    {
        var roles = await _context.Roles
            .OrderBy(r => r.Id)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.PermissionsJson))
            .ToListAsync();

        return Ok(roles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleDto>> GetById(int id)
    {
        var r = await _context.Roles.FindAsync(id);
        if (r == null) return NotFound();

        return Ok(new RoleDto(r.Id, r.Name, r.Description, r.PermissionsJson));
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleDto dto)
    {
        var role = new Role
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            PermissionsJson = dto.PermissionsJson
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = role.Id }, new RoleDto(role.Id, role.Name, role.Description, role.PermissionsJson));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateRoleDto dto)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return NotFound();

        role.Name = dto.Name.Trim();
        role.Description = dto.Description.Trim();
        role.PermissionsJson = dto.PermissionsJson;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
