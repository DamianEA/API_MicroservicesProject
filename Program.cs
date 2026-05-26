using Drive.Models;
using Drive.Services; // Asegura este uso
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Npgsql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,        // Como creaste el token a mano sin Issuer, lo dejamos en false
        ValidateAudience = false,      // Como no le pusiste Audience, lo dejamos en false
        ValidateLifetime = true,        // Para que valide la fecha "exp" de expiración
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ESTA_ES_UNA_LLAVE_SUPER_SECRETA_Y_LARGA_12345")),
        ClockSkew = TimeSpan.Zero      // Para que expire en el segundo exacto que le toca
    };
});

// --- CONFIGURACIÓN DE CORS DEFINITIVA ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowProductionAndLocal", policy =>
    {
    policy.SetIsOriginAllowed(origin => true) 
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Mantiene el soporte para tus Refresh Tokens / Cookies
    });
});

// Forzamos la conexión directa a tu Postgres nativo usando la IP puente de Docker
var connectionString = "Host=host.docker.internal;Port=5432;Database=users;Username=postgres;Password=PUTA0011";

builder.Services.AddDbContext<DefaultDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddOpenApi();

builder.Services.AddScoped<IStorageService, StorageService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

var app = builder.Build();

// Usamos la nueva política unificada (SIEMPRE antes de Authentication/Authorization)
app.UseCors("AllowProductionAndLocal");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();

app.UseAuthorization(); 

app.MapControllers();

app.UseHttpsRedirection();

app.Run();