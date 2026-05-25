using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drive.Models;
using Microsoft.AspNetCore.Http;

namespace Drive.Services
{
    public interface IStorageService
    {
        Task<FileItem> CreateFolderAsync(string name, int? parentId, int userId);
        Task<FileItem> UploadFileAsync(IFormFile file, int parentId, int userId);
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task<(List<FileItem> Items, int TotalCount)> GetDirectoryContentAsync(
            int? folderId, 
            string? searchTerm, 
            string? type, 
            string? ownerSearch, 
            DateTime? startDate, 
            DateTime? endDate, 
            int page, 
            int pageSize);

        Task<(byte[] FileBytes, string ContentType, string FileName)> DownloadFileAsync(int fileId);
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task<FileItem> RenameItemAsync(int id, string newName, int userId);
        Task DeleteItemAsync(int id, int userId);
    }
}