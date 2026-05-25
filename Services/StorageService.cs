using Drive.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Drive.Services
{
    public class StorageService : IStorageService
    {
        private readonly DefaultDbContext _context;
        private readonly string _baseStoragePath;

        public StorageService(DefaultDbContext context, IConfiguration configuration)
        {
            _context = context;
            _baseStoragePath = configuration.GetValue<string>("StorageSettings:BasePath") ?? "C:/Users/angel/Downloads/MJ";
        }
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public async Task<FileItem> CreateFolderAsync(string name, int? parentId, int userId)
        {
            var exists = await _context.Files.AnyAsync(f =>
                f.Type == "folder" &&
                f.Name.ToLower() == name.ToLower() &&
                f.ParentId == parentId);

            if (exists)
                throw new Exception("Ya existe una carpeta con ese nombre en esta ubicación.");

            var newFolder = new FileItem
            {
                Name = name,
                Type = "folder",
                IdUser = userId,
                ParentId = parentId,
                Path = "",
                SizeBytes = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.Files.Add(newFolder);
            await _context.SaveChangesAsync();
            return newFolder;
        }
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public async Task<FileItem> UploadFileAsync(IFormFile file, int parentId, int userId)
        {
            var uploadsFolder = _baseStoragePath; 
            
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (parentId <= 0)
                throw new Exception("Operación denegada. Los archivos deben subirse dentro de una carpeta, nunca en la raíz.");

            var parentFolder = await _context.Files.FirstOrDefaultAsync(f => f.Id == parentId && f.Type == "folder");
            if (parentFolder == null)
                throw new Exception("La carpeta de destino no existe.");

            long maxBytes = 10 * 1024 * 1024;
            if (file.Length > maxBytes)
                throw new Exception("El archivo es demasiado pesado. El límite estricto es de 10 MB.");

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var newFile = new FileItem
            {
                Name = file.FileName,
                Type = Path.GetExtension(file.FileName).ToLower(),
                IdUser = userId,
                ParentId = parentId,
                Path = filePath,
                SizeBytes = file.Length,
                CreatedAt = DateTime.UtcNow
            };

            _context.Files.Add(newFile);
            await _context.SaveChangesAsync();

            return newFile;
        }
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<(List<FileItem> Items, int TotalCount)> GetDirectoryContentAsync(
        int? folderId, 
        string? searchTerm, 
        string? type, 
        string? ownerSearch, 
        DateTime? startDate, 
        DateTime? endDate, 
        int page, 
        int pageSize)
    {
        var query = _context.Files.Include(f => f.Owner).AsQueryable();

        query = query.Where(f => f.ParentId == folderId);

        if (!string.IsNullOrEmpty(searchTerm))
            query = query.Where(f => f.Name.ToLower().Contains(searchTerm.ToLower()));

        if (!string.IsNullOrEmpty(type))
            query = query.Where(f => f.Type.ToLower().Contains(type.ToLower()));

        if (!string.IsNullOrEmpty(ownerSearch))
            query = query.Where(f => f.Owner != null && f.Owner.name.ToLower().Contains(ownerSearch.ToLower()));

        if (startDate.HasValue)
            query = query.Where(f => f.CreatedAt >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(f => f.CreatedAt <= endDate.Value.Date.AddDays(1).AddTicks(-1));

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(f => f.Type == "folder")
            .ThenByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public async Task<(byte[] FileBytes, string ContentType, string FileName)> DownloadFileAsync(int fileId)
        {
            var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null || file.Type == "folder")
                throw new Exception("Archivo no encontrado o es una carpeta.");

            if (!System.IO.File.Exists(file.Path))
                throw new Exception("El archivo físico no existe en el servidor.");

            var fileBytes = await System.IO.File.ReadAllBytesAsync(file.Path);
            
            return (fileBytes, "application/octet-stream", file.Name);
        }
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public async Task<FileItem> RenameItemAsync(int id, string newName, int userId)
        {
            var item = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
            if (item == null) throw new Exception("El elemento no existe.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.id == userId);
            if (user == null) throw new Exception("Usuario no válido.");

            // 🛡️ CONTROL DE ACCESO ESTRICTO
            if (item.IdUser != userId && user.role.ToLower() != "admin")
            {
                throw new Exception("Acceso denegado. No tienes permisos para renombrar este elemento.");
            }

            var exists = await _context.Files.AnyAsync(f => 
                f.ParentId == item.ParentId && 
                f.Type == item.Type && 
                f.Name.ToLower() == newName.ToLower() && 
                f.Id != id);

            if (exists) throw new Exception("Ya existe un elemento con ese nombre en esta ubicación.");

            item.Name = newName;
            await _context.SaveChangesAsync();
            return item;
        }
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public async Task DeleteItemAsync(int id, int userId)
        {
            var item = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
            if (item == null) throw new Exception("El elemento no existe.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.id == userId);
            if (user == null) throw new Exception("Usuario no válido.");

            // 🛡️ CONTROL DE ACCESO ESTRICTO
            if (item.IdUser != userId && user.role.ToLower() != "admin")
            {
                throw new Exception("Acceso denegado. No tienes permisos para eliminar este elemento.");
            }

            if (item.Type == "folder")
            {
                var allDescendants = await _context.Files
                    .Where(f => f.Path != "" && f.ParentId == id)
                    .ToListAsync();

                foreach (var file in allDescendants)
                {
                    if (File.Exists(file.Path)) File.Delete(file.Path);
                }
            }
            else
            {
                if (File.Exists(item.Path)) File.Delete(item.Path);
            }
            
            _context.Files.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}