using Microsoft.AspNetCore.Mvc;
using Drive.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Drive.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly DefaultDbContext _context;

    public UserController(DefaultDbContext context)
    {
        _context = context;
    }
    
    // GET: api/User
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? name = null,
        [FromQuery] string? email = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {         
        try
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                string term = name.Trim();
                query = query.Where(u => u.name != null && EF.Functions.ILike(u.name, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                string term = email.Trim();
                query = query.Where(u => u.email != null && EF.Functions.ILike(u.email, $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(role) && role != "Todos")
            {
                query = query.Where(u => u.role == role.Trim());
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "Todos")
            {
                query = query.Where(u => u.status == status.Trim());
            }

            List<User> users = await query
                .OrderBy(u => u.name) 
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al obtener la lista de usuarios.", error = ex.Message });
        }
    }

    // GET: api/User/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {        
        User? user = await _context.Users.FirstOrDefaultAsync(u => u.id == id);

        if (user == null)
            return NotFound(new { message = "Usuario no encontrado." });

        return Ok(user);
    }

    // POST: api/User/register
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUser userData)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var emailExiste = await _context.Users.AnyAsync(u => u.email.ToLower() == userData.email.ToLower().Trim());
        if (emailExiste)
        {
            return BadRequest(new { message = "El correo ya se encuentra registrado." });
        }

        var newUser = new User
        {
            name = userData.name ?? string.Empty,       
            email = userData.email ?? string.Empty,
            pass = Drive.Models.User.GetHash(userData.pass ?? string.Empty), 
            birth = DateTime.SpecifyKind(userData.birth, DateTimeKind.Utc),
            role = string.IsNullOrEmpty(userData.role) ? "User" : userData.role,
            status = string.IsNullOrEmpty(userData.status) ? "Desactivado" : userData.status
        };

        try 
        {
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUserById), new { id = newUser.id }, newUser);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // PUT: api/User/5
    // ACTUALIZA DATOS ESENCIALES, ROL Y ESTADO DESDE EL PANEL DE ADMINISTRACIÓN
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto userData)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Usuario no encontrado." });

        // Validar duplicidad de email si este cambió
        if (user.email.ToLower() != userData.email.ToLower().Trim())
        {
            var emailExiste = await _context.Users.AnyAsync(u => u.email.ToLower() == userData.email.ToLower().Trim() && u.id != id);
            if (emailExiste) return BadRequest(new { message = "El correo ya pertenece a otro usuario." });
        }

        user.name = userData.name ?? user.name;
        user.email = userData.email ?? user.email;
        user.role = userData.role ?? user.role;
        user.status = userData.status ?? user.status;
        user.birth = DateTime.SpecifyKind(userData.birth, DateTimeKind.Utc);

        // CORRECCIÓN: Asignación con mayúscula inicial respetando tu modelo User
        user.UpdatedAt = DateTime.UtcNow; 

        if (!string.IsNullOrWhiteSpace(userData.pass))
        {
            user.pass = Drive.Models.User.GetHash(userData.pass);
        }

        try
        {
            await _context.SaveChangesAsync();
            return Ok(user);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    // ACCIÓN PARA BORRAR USUARIOS TOTALMENTE DE LA PLATAFORMA
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "El usuario no existe." });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Usuario eliminado correctamente." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "No se puede eliminar el usuario. Comprueba si tiene archivos vinculados.", error = ex.Message });
        }
    }
}

// DTO para la actualización segura de datos de usuario
public class UpdateUserDto
{
    public string name { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string? pass { get; set; }
    public DateTime birth { get; set; }
    public string role { get; set; } = "User";
    public string status { get; set; } = "Activo";
}