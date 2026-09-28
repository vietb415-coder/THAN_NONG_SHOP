using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 

namespace THAN_NONG_SHOP.Models
{
    public class user
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(100)] public string UserName { get; set; } = "";
        [Required, MaxLength(100)] public string Fullname { get; set; } = "";

        [Required, MaxLength(254)] public string Email { get; set; } = "";
        [MaxLength(254)] public string NormalizedEmail { get; private set; } = "";
        public string Password { get; set; } = "";
        [Required, MaxLength(20)] public string Phone { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool EmailConfirmed { get; set; }
        [MaxLength(64)] public string? ConfirmationTokenHash { get; set; }
        public DateTime? ConfirmationExpiresAt { get; set; }
        public bool SellerRequested { get; set; }
        public bool SellerApproved { get; set; }
        
        public int RoleId { get; set; }
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }      
    }
}
