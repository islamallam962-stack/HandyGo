using HandyGo.web.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HandyGo.web.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public int RequestId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public string Text { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        [ForeignKey("RequestId")]
        public Request Request { get; set; }

        [ForeignKey("SenderId")]
        public User Sender { get; set; }

        public bool IsSeen { get; set; }

        public string? ImagePath { get; set; }


    }
}


