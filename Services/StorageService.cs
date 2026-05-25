using Drive.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            var query = _context.Files.AsQueryable();

            if (string.IsNullOrEmpty(searchTerm))
            {
                if (folderId == null || folderId == 0)
                {
                    query = query.Where(f => f.ParentId == null);
                }
                else
                {
                    query = query.Where(f => f.ParentId == folderId);
                }
            }
            else
            {
                string term = searchTerm.Trim();
                query = query.Where(f => f.Name != null && EF.Functions.ILike(f.Name, $"%{term}%"));
            }

            if (!string.IsNullOrEmpty(type))
            {
                string t = type.Trim();
                query = query.Where(f => f.Type != null && EF.Functions.ILike(f.Type, $"%{t}%"));
            }

            if (!string.IsNullOrEmpty(ownerSearch))
            {
                string ownerTerm = ownerSearch.Trim();
                var matchingUserIds = await _context.Users
                    .Where(u => u.name != null && EF.Functions.ILike(u.name, $"%{ownerTerm}%"))
                    .Select(u => u.id)
                    .ToListAsync();

                query = query.Where(f => matchingUserIds.Contains(f.IdUser));
            }

            if (startDate.HasValue && startDate.Value != DateTime.MinValue)
            {
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(f => f.CreatedAt >= start);
            }

            if (endDate.HasValue && endDate.Value != DateTime.MinValue)
            {
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(f => f.CreatedAt <= end);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(f => f.Type == "folder" ? 1 : 0)
                .ThenByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (items.Any())
            {
                var userIds = items.Select(i => i.IdUser).Distinct().ToList();
                var users = await _context.Users.Where(u => userIds.Contains(u.id)).ToListAsync();

                foreach (var item in items)
                {
                    item.Owner = users.FirstOrDefault(u => u.id == item.IdUser);
                }
            }

            return (items, totalCount);
        }

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

        // RenamedItemAsync - COMPLETAMENTE CORREGIDO CONTRA VALIDACIÓN DE ROLES Y TRACKING
        public async Task<FileItem> RenameItemAsync(int id, string newName, int userId)
        {
            var item = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
            if (item == null) throw new Exception("El elemento no existe.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.id == userId);
            if (user == null) throw new Exception("Usuario no válido.");

            // Sanitización del rol en minúsculas y prevención de nulos
            string userRole = user.role?.ToLower() ?? "";
            if (item.IdUser != userId && userRole != "administrador" && userRole != "admin")
            {
                throw new Exception("Acceso denegado. No tienes permisos para renombrar este elemento.");
            }

            var exists = await _context.Files.AnyAsync(f => 
                f.ParentId == item.ParentId && 
                f.Type == item.Type && 
                f.Name.ToLower() == newName.ToLower() && 
                f.Id != id);

            if (exists) throw new Exception("Ya existe un elemento con ese nombre en esta ubicación.");

            // Modificación y marcado de estado explícito para forzar la actualización en Base de Datos
            item.Name = newName;
            _context.Entry(item).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return item;
        }

        public async Task DeleteItemAsync(int id, int userId)
        {
            var item = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);
            if (item == null) throw new Exception("El elemento no existe.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.id == userId);
            if (user == null) throw new Exception("Usuario no válido.");

            string userRole = user.role?.ToLower() ?? "";
            if (item.IdUser != userId && userRole != "administrador" && userRole != "admin")
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