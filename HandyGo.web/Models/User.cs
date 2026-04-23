using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;
using System.Collections.Generic;

namespace HandyGo.web.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MinLength(3)]
        [RegularExpression(@"^[a-zA-Z\u0621-\u064A\s]+$", ErrorMessage = "«·«”„ ÌÃ» √‰ ÌÕ ÊÌ ⁄·Ï Õ—Ê› ›ﬁÿ")]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [RegularExpression(@"^\d{11}$", ErrorMessage = "—ﬁ„ «·Â« › ÌÃ» √‰ ÌﬂÊ‰ 11 —ﬁ„")]
        public string? Phone { get; set; }

        public string? PasswordHash { get; set; }

        public string AuthProvider { get; set; } = "Local";
        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordExpiry { get; set; }

        [Required]
        public string Role { get; set; } 

        public string? Category { get; set; }

        public string? ImagePath { get; set; }
        public string? Skills { get; set; }
        public string? Certificates { get; set; }
        public string? Address { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime? LocationUpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsTopRated { get; set; } = false;
        public DateTime? LastSeen { get; set; } = DateTime.Now;

        public string? AdminNote { get; set; }
        public DateTime? AdminNoteDate { get; set; }



        public DateTime CreatedAt { get; set; } = DateTime.Now;



        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; } = 0; 




        public string? ReferralCode { get; set; }

        public int? ReferredByUserId { get; set; }




        public string? SubscriptionPlan { get; set; }

        public DateTime? SubscriptionExpiry { get; set; }




        public string? IdCardImage { get; set; }

        public string? CertificateImage { get; set; }

        public string ?VerificationStatus { get; set; } = "Unverified";

        public string? VerificationRejectionReason { get; set; }

        [InverseProperty("Client")]
        public virtual ICollection<Request> ClientRequests { get; set; } = new List<Request>();

        [InverseProperty("Technician")]
        public virtual ICollection<Request> TechnicianRequests { get; set; } = new List<Request>();

        [InverseProperty("Technician")]
        public virtual ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
    }
}
