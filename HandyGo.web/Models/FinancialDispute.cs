using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandyGo.web.Models
{
    public class FinancialDispute
    {
        public int Id { get; set; }

        public int RequestId { get; set; }
        public int InitiatorUserId { get; set; } 

        [Required]
        public string Reason { get; set; } 

        [Required]
        public string Details { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Open";

        public string? AdminNote { get; set; } 

        [ForeignKey("RequestId")]
        public Request? Request { get; set; }

        [ForeignKey("InitiatorUserId")]
        public User? InitiatorUser { get; set; }
    }
}
