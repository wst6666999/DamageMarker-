using System;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DamageMaker.Models
{
    public class SqlImgInfo : ObservableObject
    {
        public long ImgId { get; set; }
        public string ImgPath { get; set; }
        public long FolderId { get; set; }
        public bool? IsConfirmDamage { get; set; }
        public string? Remark { get; set; }

        // 新增：用于存储图片二进制数据
        public byte[]? ImageData { get; set; }

        public string? Mileage { get; set; }
        public DateTime SaveTime { get; set; } = DateTime.Now;

        private BitmapImage _imgData;
        public BitmapImage ImgData
        {
            get => _imgData;
            set => SetProperty(ref _imgData, value);
        }
        public string? DamageType { get; set; }
        public bool IsSelected { get; set; }

        private int? _damageLevel = null; // 新增：伤损等级，默认I级
        public int? DamageLevel
        {
            get => _damageLevel;
            set => SetProperty(ref _damageLevel, value);

        }
    }
}