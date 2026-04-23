using System.ComponentModel.DataAnnotations;
using System;

namespace HandyGo.web.Models
{
    public class PromoCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } 

        [Required]
        public string DiscountType { get; set; } 

        [Required]
        public decimal DiscountValue { get; set; } 

        public DateTime ExpiryDate { get; set; } 

        public bool IsActive { get; set; } = true; 

        public int MaxUsageCount { get; set; } = 100; 

        public int CurrentUsageCount { get; set; } = 0; 
    }
}
