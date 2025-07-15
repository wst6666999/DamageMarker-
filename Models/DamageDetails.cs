using System.Windows.Media;

namespace DamageMaker.Models
{
    public class DamageDetails
    {
        public int Id { get; set; }
        public SolidColorBrush MarkerColor { get; set;}
        public string DamageCategory { get; set;}
        public string Similarity { get; set;}

    }
}
