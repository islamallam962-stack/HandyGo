using System;
using System.ComponentModel.DataAnnotations;

namespace HandyGo.web.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; } 

        [Required]
        [StringLength(500)]
        public string Message { get; set; } 

        public string Link { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false; 
    }
}
