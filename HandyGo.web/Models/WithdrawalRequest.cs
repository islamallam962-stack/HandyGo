using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandyGo.web.Models
{
    public class WithdrawalRequest
    {
        public int Id { get; set; }

        public int TechnicianId { get; set; }
        [ForeignKey("TechnicianId")]
        public virtual User Technician { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Method { get; set; } 

        [Required]
        public string Details { get; set; } 

        public string Status { get; set; } = "Pending"; 

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
