using System.ComponentModel.DataAnnotations;

namespace HandyGo.web.ViewModels
{
    public class UpgradeToTechnicianViewModel
    {
        [Required(ErrorMessage = "ÑŞã ÇáåÇÊİ ãØáæÈ ááÊæÇÕá ãÚ ÇáÚãáÇÁ")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "ÑŞã ÇáåÇÊİ íÌÈ Ãä íßæä 11 ÑŞã")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "íÌÈ ÇÎÊíÇÑ ÇáÊÎÕÕ Çáİäí")]
        public string Category { get; set; }

        public string? Skills { get; set; }
        public string? Certificates { get; set; }

        [Required(ErrorMessage = "íÑÌì ßÊÇÈÉ ÚäæÇäß ÇáÊİÕíáí")]
        public string Address { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
