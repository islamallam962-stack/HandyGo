using System.ComponentModel.DataAnnotations;

namespace HandyGo.web.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "ﬂ·„… «·„—Ê— «·ÃœÌœ… „ÿ·Ê»…")]
        [MinLength(8, ErrorMessage = "ÌÃ» √‰  ﬂÊ‰ 8 √Õ—› ⁄·Ï «·√ﬁ·")]
        public string NewPassword { get; set; }

        [Compare("NewPassword", ErrorMessage = "ﬂ·„«  «·„—Ê— €Ì— „ ÿ«»ﬁ…")]
        public string ConfirmPassword { get; set; }
    }
}
