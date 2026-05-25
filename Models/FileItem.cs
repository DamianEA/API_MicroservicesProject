using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Drive.Models
{
    [Table("files")]
    public class FileItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("path")]
        public string Path { get; set; } = string.Empty;

        [Required]
        [Column("id_user")]
        public int IdUser { get; set; }

        [Required]
        [Column("type")]
        public string Type { get; set; } = string.Empty; 

        [Column("parent_id")]
        public int? ParentId { get; set; } 

        [Column("size_bytes")]
        public long SizeBytes { get; set; } = 0; 

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        // Hidratado manualmente desde StorageService, se ignora en la BD
        [NotMapped]
        public User? Owner { get; set; }
    }
}