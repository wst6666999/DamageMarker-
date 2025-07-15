namespace DamageMarker.Models
{
    public class ImgInfo
    {
        public string Path { get; set; }
        public string? Name { get; set; }

        public ImgInfo Clone()
        {
            return new ImgInfo
            {
                Path = Path,
                Name = Name
            };
        }
    }
}
