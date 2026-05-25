using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;

namespace Drive.Models;
[Table("users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int id { get; set; }

    [Required]  
    [Column("name")]
    public string name { get; set; } = null!;

    [Required]
    [Column("email")]
    public string email { get; set; } = null!;

    [Required]
    [Column("pass")]
    public string pass { get; set; } = null!;

    private DateTime _birth;
    [Required]
    [Column("birth")]
    public DateTime birth 
    { 
        get => _birth; 
        set => _birth = DateTime.SpecifyKind(value, DateTimeKind.Utc); 
    }
    
    [Required]
    [Column("role")]
    public string role { get; set; } = "User";
    [Column("status")]
    public string status { get; set; } = "Desactivado";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    [Column("refreshtoken")]
    public string? RefreshToken { get; set; }

    [Column("refreshtokenexpirytime")]
    public DateTime? RefreshTokenExpiryTime { get; set; }
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    
    public static string GetHash(string input)
{
    byte[] inputBytes = Encoding.UTF8.GetBytes(input);
    byte[] hashedBytes = MD5.HashData(inputBytes);
        
    return Convert.ToHexString(hashedBytes).ToLower(); 
}
}

public class UserCredentials
{   
    [Required]
    public string email { get; set; } = null!;

    [Required]
    public string pass { get; set; } = null!;
}

public class CreateUser
{
    [Required]
    public string name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "La dirección no pertenece a un dirección de correo válida")]
    [Required(ErrorMessage = "El campo es obligatorio")]
    public string email { get; set; } = string.Empty;

    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Required]
    public DateTime birth { get; set; }

    [DataType(DataType.Password)]
    [Required]
    public string pass { get; set; } = string.Empty;

    
    [Compare("pass", ErrorMessage = "Las contraseñas no coinciden")]
    [DisplayName("Password Confirm")]
    public string PasswordConfirm { get; set; } = string.Empty;
    
    [Required]
    public string role { get; set; } = string.Empty;
    public string? status { get; set; } = "Desactivado";

}