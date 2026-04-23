using HandyGo.web.Models;
using System.ComponentModel.DataAnnotations.Schema;

public class Bid
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    [ForeignKey("RequestId")]
    public virtual Request Request { get; set; }

    public int TechnicianId { get; set; }
    [ForeignKey("TechnicianId")]
    public virtual User Technician { get; set; }

    public decimal Price { get; set; } 
    public string Note { get; set; }  
    public string EstimatedTime { get; set; } 
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
