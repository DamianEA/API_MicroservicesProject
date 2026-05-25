using Microsoft.AspNetCore.Mvc;
using Drive.Services;
using Drive.Models;
using System;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Drive.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StorageController : ControllerBase
{
    private readonly IStorageService _storageService;

    public StorageController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    // 1. GET: api/Storage/directory (LISTAR)
    [HttpGet("directory")]
    public async Task<IActionResult> GetDirectory(
        [FromQuery] int? folderId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? type,
        [FromQuery] string? ownerSearch,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var (items, totalCount) = await _storageService.GetDirectoryContentAsync(
                folderId, searchTerm, type, ownerSearch, startDate, endDate, page, pageSize);

            return Ok(new { items, totalCount });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    // 2. POST: api/Storage/folder (CREAR CARPETA)
    // 2. POST: api/Storage/folder (CREAR CARPETA)
    [HttpPost("folder")]
    public async Task<IActionResult> CreateFolder([FromBody] HttpCreateFolderRequest model)
    {
        try
        {
            // CORRECCIÓN: Se cambió model.name por model.Name
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { message = "El nombre de la carpeta es requerido." });
            }
            // CORRECCIÓN: Se cambió model.userId por model.UserId
            if (!model.UserId.HasValue || model.UserId <= 0)
            {
                return BadRequest(new { message = "El campo 'userId' es requerido y debe ser válido." });
            }

            // CORRECCIÓN: Se cambiaron todas a Mayúsculas: Name, ParentId, UserId
            var newFolder = await _storageService.CreateFolderAsync(model.Name.Trim(), model.ParentId, model.UserId.Value);
            return Ok(newFolder);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    // 3. POST: api/Storage/file (SUBIR ARCHIVO)
    [HttpPost("file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] int? parentId, [FromForm] int userId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No se ha seleccionado ningún archivo válido." });
            }
            if (userId <= 0)
            {
                return BadRequest(new { message = "El campo 'userId' es requerido para subir archivos." });
            }

            int folderId = parentId.GetValueOrDefault(0); 

            var uploadedFile = await _storageService.UploadFileAsync(file, folderId, userId);
            return Ok(uploadedFile);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    // 4. PUT: api/Storage/{id}/rename (RENOMBRAR) - CORREGIDO PARA ENTRADA EN MINÚSCULAS
    // 4. PUT: api/Storage/{id}/rename (RENOMBRAR)
    [HttpPut("{id}/rename")]
    public async Task<IActionResult> RenameFile(int id, [FromBody] HttpRenameRequest model)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "El id del archivo en la URL es inválido." });
            }
            // CORRECCIÓN: Usamos las propiedades con mayúscula inicial
            if (model == null || string.IsNullOrWhiteSpace(model.Name)) 
            {
                return BadRequest(new { message = "El campo 'name' no puede estar vacío." });
            }
            if (model.UserId <= 0) 
            {
                return BadRequest(new { message = "El campo 'userId' es requerido y debe ser válido." });
            }

            var updatedItem = await _storageService.RenameItemAsync(id, model.Name.Trim(), model.UserId);
            return Ok(updatedItem);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    // 5. DELETE: api/Storage/{id} (ELIMINAR)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(int id, [FromQuery] int userId)
    {
        try
        {
            await _storageService.DeleteItemAsync(id, userId);
            return Ok(new { message = "Elemento eliminado con éxito." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    // 6. GET: api/Storage/download/{id} (DESCARGAR)
    [HttpGet("download/{id}")]
    public async Task<IActionResult> DownloadFile(int id)
    {
        try
        {
            var (fileBytes, contentType, fileName) = await _storageService.DownloadFileAsync(id);
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, inner = ex.InnerException?.Message });
        }
    }
}

// =========================================================================
// DTOs CORREGIDOS CON MINÚSCULAS PARA ASEGURAR EL BINDING DIRECTO DESDE REACT
// =========================================================================


public class HttpRenameRequest
{
    
    public string Name { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public class HttpCreateFolderRequest
{
    public string? Name { get; set; }
    public int? ParentId { get; set; }
    public int? UserId { get; set; }
}