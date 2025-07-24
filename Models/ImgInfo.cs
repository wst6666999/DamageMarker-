namespace DamageMarker.Models
{
    public class ImgInfo
    {
        public string Path { get; set; }
        public string? Name { get; set; }

        public bool IsTestTrack { get; set; }     // 是否测试轨道图片
        public string TestTrackType { get; set; } // 测试轨道类型（Before/After）

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
