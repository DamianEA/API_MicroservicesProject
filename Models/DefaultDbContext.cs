using Microsoft.EntityFrameworkCore;

namespace Drive.Models;

public class DefaultDbContext(DbContextOptions<DefaultDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<FileItem> Files { get; set; } = null!; // O como se llame tu entidad de archivos
}