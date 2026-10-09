namespace Floreria.API.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PermissionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

public class Attendance
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Date { get; set; } = string.Empty; // YYYY-MM-DD
    public string? ClockIn { get; set; }             // HH:mm
    public string? ClockOut { get; set; }            // HH:mm
    public string Status { get; set; } = "Presente"; // Presente, Retardo, Falta, Justificado, Permiso
    public string? Notes { get; set; }
    public string? DeviceFingerprint { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
