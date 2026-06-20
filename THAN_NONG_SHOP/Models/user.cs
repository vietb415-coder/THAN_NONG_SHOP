using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 

namespace THAN_NONG_SHOP.Models
{
    public class user
    {
        [Key]
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Fullname { get; set; }

        public string Email { get; set; }
        public string Password { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public int RoleId { get; set; }
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }      
    }
}
