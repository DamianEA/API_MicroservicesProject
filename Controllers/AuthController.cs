using Microsoft.AspNetCore.Mvc;
using Drive.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization; // 1. Agregamos esto para los permisos

namespace Drive.Controllers;

[AllowAnonymous] // 2. Dejamos que TODOS pasen al Login y al RefreshToken sin gafete
[Route("api/[controller]")] // 3. ADIÓS al [Route("api/User")] que chocaba con tu otro archivo. Ahora es solo api/Auth
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DefaultDbContext _context;

    public AuthController(DefaultDbContext context)
    {
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(UserCredentials userCredentials)
    {
        if(ModelState.IsValid)
        {
            var user = _context.Users.FirstOrDefault(u => u.email.ToLower() == userCredentials.email.ToLower().Trim());

            if (user != null)
            {
                string computedHash = Models.User.GetHash(userCredentials.pass);
                string dbHash = user.pass.Replace("-", "").ToLower();

                if (computedHash == dbHash)
                {
                    if (user.status != "Activo")
                    {
                        return Unauthorized(new { message = "Usuario inactivo. Requiere activación de un administrador." });
                    }

                    // 4. CREAMOS LA FECHA DE CADUCIDAD (15 MINUTOS)
                    long exp = new DateTimeOffset(DateTime.UtcNow.AddMinutes(15)).ToUnixTimeSeconds();

                    string headerJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
                    string payloadJson = $"{{" +
                        $"\"id\":\"{user.id}\"," +
                        $"\"email\":\"{user.email}\"," +
                        $"\"role\":\"{user.role}\"," +
                        $"\"exp\":{exp}," + // <-- 5. ¡AQUÍ ESTÁ LA MAGIA! Le pegamos la caducidad al gafete
                        $"\"http://schemas.microsoft.com/ws/2008/06/identity/claims/role\":\"{user.role}\"," +
                        $"\"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier\":\"{user.id}\"" +
                    $"}}";

                    string base64Header = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                    string base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                    string unsignedToken = $"{base64Header}.{base64Payload}";

                    byte[] keyBytes = Encoding.UTF8.GetBytes("ESTA_ES_UNA_LLAVE_SUPER_SECRETA_Y_LARGA_12345");
                    using (var hmac = new HMACSHA256(keyBytes))
                    {
                        byte[] tokenBytes = Encoding.UTF8.GetBytes(unsignedToken);
                        byte[] hashBytes = hmac.ComputeHash(tokenBytes);
                        string base64Signature = Convert.ToBase64String(hashBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                        string realToken = $"{unsignedToken}.{base64Signature}";

                        string refreshToken = Guid.NewGuid().ToString();
                        user.RefreshToken = refreshToken;
                        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                        _context.SaveChanges();

                        return Ok(new
                        {
                            token = realToken,
                            refreshToken = refreshToken,
                            id = user.id,
                            name = user.name,
                            email = user.email,
                            role = user.role
                        });
                    }
                }
            }
        }

        return Unauthorized(new { message = "Credenciales incorrectas o usuario inexistente." });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.refreshToken))
        {
            return BadRequest(new { message = "Petición inválida." });
        }

        var user = _context.Users.FirstOrDefault(u => u.RefreshToken == request.refreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "El Refresh Token es inválido o ha expirado. Inicie sesión nuevamente." });
        }

        // VOLVEMOS A CALCULAR LA CADUCIDAD PARA EL NUEVO TOKEN (Otros 15 minutos)
        long exp = new DateTimeOffset(DateTime.UtcNow.AddMinutes(15)).ToUnixTimeSeconds();

        string headerJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        string payloadJson = $"{{" +
            $"\"id\":\"{user.id}\"," +
            $"\"email\":\"{user.email}\"," +
            $"\"role\":\"{user.role}\"," +
            $"\"exp\":{exp}," + // <-- Se lo pegamos también al nuevo token
            $"\"http://schemas.microsoft.com/ws/2008/06/identity/claims/role\":\"{user.role}\"," +
            $"\"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier\":\"{user.id}\"" +
        $"}}";

        string base64Header = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string unsignedToken = $"{base64Header}.{base64Payload}";

        byte[] keyBytes = Encoding.UTF8.GetBytes("ESTA_ES_UNA_LLAVE_SUPER_SECRETA_Y_LARGA_12345");
        using (var hmac = new HMACSHA256(keyBytes))
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(unsignedToken);
            byte[] hashBytes = hmac.ComputeHash(tokenBytes);
            string base64Signature = Convert.ToBase64String(hashBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string realToken = $"{unsignedToken}.{base64Signature}";

            string newRefreshToken = Guid.NewGuid().ToString();
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            
            _context.SaveChanges();

            return Ok(new
            {
                token = realToken,
                refreshToken = newRefreshToken
            });
        }
    }
}

public class TokenRequest
{
    public string accessToken { get; set; } = string.Empty;
    public string refreshToken { get; set; } = string.Empty;
}