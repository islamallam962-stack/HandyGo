using System.ComponentModel.DataAnnotations;

namespace HandyGo.web.Models
{
    public class Review
    {
        public int Id { get; set; }

        [Required]
        public int RequestId { get; set; }

        public virtual User Technician { get; set; }
        [Required]
        public int TechnicianId { get; set; } 

        [Range(1, 5)]
        public int Stars { get; set; } 

        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Request Request { get; set; }
    }
}
