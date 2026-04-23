using HandyGo.web.Models;
using System.ComponentModel.DataAnnotations;

public class Complaint
{
    public int Id { get; set; }

    [Required]
    public string Subject { get; set; } 

    [Required]
    public string Description { get; set; } 

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsResolved { get; set; } = false; 

    public int RequestId { get; set; }
    public virtual Request Request { get; set; }
}
