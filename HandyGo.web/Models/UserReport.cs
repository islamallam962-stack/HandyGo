using HandyGo.web.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandyGo.web.Models
{
    public class UserReport
    {
        public int Id { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

       
        public int ReporterId { get; set; }
        public virtual User Reporter { get; set; }

       
        public int ReportedUserId { get; set; }
        public virtual User ReportedUser { get; set; }
    }
}
