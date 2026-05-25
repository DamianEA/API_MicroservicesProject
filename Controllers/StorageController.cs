using Microsoft.AspNetCore.Mvc;
using Drive.Services;

namespace Drive.Controllers
{
[ApiController]
[Route("api/[controller]")]
public class StorageController : ControllerBase
{
    private readonly IStorageService _storageService;

    // Inyectamos el servicio por el constructor
    public StorageController(IStorageService storageService)
    {
        _storageService = storageService;
    }
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
[HttpPost("folder")]
public async Task<IActionResult> CreateFolder([FromBody] CreateFolderDto request)
{
    try
    {
        var folder = await _storageService.CreateFolderAsync(request.Name, request.ParentId, request.UserId);
        return Ok(folder);
    }
    catch (Exception ex)
    {
        // Si rompe alguna regla (ej. carpeta duplicada), devolvemos el error al Front
        return BadRequest(new { message = ex.Message }); 
    }
}
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Usamos [FromForm] porque los archivos viajan como FormData, no como JSON normal
[HttpPost("file")]
public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] int parentId, [FromForm] int userId)
{
    try
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No se envió ningún archivo válido." });

        var uploadedFile = await _storageService.UploadFileAsync(file, parentId, userId);
        return Ok(uploadedFile);
    }
    catch (Exception ex)
    {
        // Si rompe reglas (ej. pesa más de 10MB o no tiene parentId), atrapamos el error
        return BadRequest(new { message = ex.Message });
    }
}
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
[HttpGet("directory")]
public async Task<IActionResult> GetDirectory(
    [FromQuery] int? folderId, 
    [FromQuery] string? search, 
    [FromQuery] string? type, 
    [FromQuery] string? owner, 
    [FromQuery] DateTime? startDate, 
    [FromQuery] DateTime? endDate, 
    [FromQuery] int page = 1, 
    [FromQuery] int pageSize = 10)
{
    try
    {
        var (items, totalCount) = await _storageService.GetDirectoryContentAsync(
            folderId, search, type, owner, startDate, endDate, page, pageSize
        );

        return Ok(new { items, total = totalCount });
    }
    catch (Exception ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
[HttpGet("download/{id}")]
public async Task<IActionResult> DownloadFile(int id)
{
    try
    {
        var (fileBytes, contentType, fileName) = await _storageService.DownloadFileAsync(id);
        // Retorna el archivo como un adjunto descargable
        return File(fileBytes, contentType, fileName); 
    }
    catch (Exception ex)
    {
        return NotFound(new { message = ex.Message });
    }
}
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
[HttpPut("{id}/rename")]
public async Task<IActionResult> Rename(int id, [FromBody] RenameDto request)
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.NewName))
            return BadRequest(new { message = "El nuevo nombre no puede estar vacío." });

        var updatedItem = await _storageService.RenameItemAsync(id, request.NewName, request.UserId);
        return Ok(updatedItem);
    }
    catch (Exception ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteItem(int id, [FromQuery] int userId)
{
    if (userId <= 0)
    {
        return Unauthorized(new { message = "Seguridad: No se detectó un ID de usuario activo." });
    }

    try
    {
        await _storageService.DeleteItemAsync(id, userId);
        return Ok(new { message = "Eliminado con éxito." });
    }
    catch (Exception ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
}
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DTO sencillo para la petición de renombrado
public class RenameDto
{
    public string NewName { get; set; } = string.Empty;
    public int UserId { get; set; }
}
public class CreateFolderDto
{
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int UserId { get; set; }
}
}