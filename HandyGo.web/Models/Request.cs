using HandyGo.web.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandyGo.web.Models
{
    public class Request
    {
        public virtual ICollection<Bid> Bids { get; set; } = new List<Bid>();
        public int Id { get; set; }

        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual User Client { get; set; }

        public int? TechnicianId { get; set; }
        [ForeignKey("TechnicianId")]
        public virtual User Technician { get; set; }

        [Required]
        public string ServiceType { get; set; }

        [Required]
        public string Address { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? LocationUpdatedAt { get; set; }

        [Required]
        public string Status { get; set; }



        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; } 

        public string PaymentStatus { get; set; } = "Pending"; 

        public string? PaymentMethod { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PlatformCommission { get; set; } 

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NetToTechnician { get; set; } 


        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Review Review { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? ActualStartTime { get; set; }
    }
}
