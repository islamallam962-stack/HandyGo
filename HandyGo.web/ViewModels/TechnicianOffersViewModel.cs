namespace HandyGo.web.Models
{
    public class TechnicianOffersViewModel
    {
        public User Technician { get; set; }
        public double AverageRating { get; set; }
        public double Distance { get; set; }

        public int ReviewCount { get; set; }
        public List<Review> Reviews { get; set; }
    }
}
