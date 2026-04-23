using System.ComponentModel.DataAnnotations;

namespace HandyGo.web.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "«·«”„ „ÿ·Ê»")]
        [MinLength(3)]
        [RegularExpression(@"^[a-zA-Z\u0621-\u064A\s]+$", ErrorMessage = "«·«”„ ÌÃ» √‰ ÌÕ ÊÌ ⁄·Ï Õ—Ê› ›ﬁÿ")]
        public string Name { get; set; }

        [Required(ErrorMessage = "«·»—Ìœ «·≈·ﬂ —Ê‰Ì „ÿ·Ê»")]
        [EmailAddress(ErrorMessage = "»—Ìœ ≈·ﬂ —Ê‰Ì €Ì— ’«·Õ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "—ﬁ„ «·Â« › „ÿ·Ê»")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "—ﬁ„ «·Â« › ÌÃ» √‰ ÌﬂÊ‰ 11 —ﬁ„")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "ﬂ·„… «·„—Ê— „ÿ·Ê»…")]
        [MinLength(8, ErrorMessage = "ﬂ·„… «·„—Ê— ·«  ﬁ· ⁄‰ 8 √Õ—›")]
        public string Password { get; set; }

        [Required(ErrorMessage = "ÌÃ» «Œ Ì«— ‰Ê⁄ «·Õ”«»")]
        public string Role { get; set; }

        public string? Category { get; set; }

        public string? ReferralCodeInput { get; set; }
    }
}
