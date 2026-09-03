//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Media;
//using CommunityToolkit.Mvvm.ComponentModel;

//namespace DamageMaker.Models
//{
//    public interface ITreeNode
//    {
//        string Name { get; set; }
//    }

//    public class DamageCategorySummaryTree:ITreeNode
//    {
//        public string Name { get; set; }
//        public List<ITreeNode> Children { get; set; }
//    }

//    public class DamageCategoryTree : ITreeNode
//    {
//        public string Name { get; set; }

//        public SolidColorBrush ColorBrush { get; set; }
//        public int ? Count { get; set; }
//        public List<Details> Children { get; set;}
//        public bool IsSelected { get; set; }
//    }
//    public partial class Details : ObservableObject
//    {
//        static int DetailsCount = 1;
//        public Details()
//        {
//            Id = DetailsCount++;
//        }

//        public int? Id { get; set; }
//        public string FileName { get; set; }
//        public int Count { get; set; }
//        public float? weight { get; set; }

//        // 添加显示序号属性
//        [ObservableProperty]
//        private int _displayIndex;

//        // 在 Details 类中添加
//        [ObservableProperty]
//        [NotifyPropertyChangedFor(nameof(StatusColor))]
//        public DamageStatus _status;

//        public Brush StatusColor
//        {
//            get
//            {
//                var brush = this switch
//                {
//                    { Status: DamageStatus.HASDAMAGE } => Brushes.LightCoral,
//                    { Status: DamageStatus.NODAMAGE } => Brushes.LightGreen,
//                    { Status: DamageStatus.KNOWN } => Brushes.Yellow,
//                    _ => Brushes.Transparent
//                };
//                return brush;
//            }
//        }

//        [ObservableProperty]
//        private bool _isSelected;
//        public string _tempImagePath { get; set; }
//    }
//}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DamageMaker.Models
{
    public interface ITreeNode
    {
        string Name { get; set; }
        bool IsExpanded { get; set; } // 添加展开状态
        bool IsSelected { get; set; } // 添加选中状态
    }

    public class DamageCategorySummaryTree : ITreeNode, INotifyPropertyChanged
    {
        public string Name { get; set; }
        public List<ITreeNode> Children { get; set; }

        // 展开状态
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        // 选中状态（用于 TreeView 导航）
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // ✅ 新增：用于 CheckBox 多选的属性
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    // 可选：触发子节点同步
                    if (Children != null)
                    {
                        foreach (var child in Children.OfType<DamageCategoryTree>())
                        {
                            child.IsChecked = value;
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class DamageCategoryTree : ITreeNode, INotifyPropertyChanged
    {
        public string Name { get; set; }
        public SolidColorBrush ColorBrush { get; set; }
        public int? Count { get; set; }
        public List<Details> Children { get; set; }

        // 展开状态
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        // 选中状态（用于 TreeView 导航）
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // ✅ 新增：用于 CheckBox 多选的属性
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    // 可选：同步子节点状态
                    if (Children != null)
                    {
                        foreach (var child in Children)
                        {
                            child.IsChecked = value;
                        }
                    }
                }
            }
        }

        public string _tempImagePath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class Details : ObservableObject, ITreeNode
    {
        static int DetailsCount = 1;
        public Details()
        {
            Id = DetailsCount++;
        }

        public int? Id { get; set; }
        public string FileName { get; set; }
        public int Count { get; set; }
        public float? weight { get; set; }

        private string _weightText;
        public string WeightText
        {
            get => string.IsNullOrEmpty(_weightText)
                ? (weight?.ToString("F2") ?? "")
                : _weightText;
            set => SetProperty(ref _weightText, value);
        }
        public float? length { get; set; }//长度

        [ObservableProperty]
        private int _displayIndex;

        // ✅ 新增：用于 CheckBox 多选的属性
        [ObservableProperty]
        private bool _isChecked;

        public string _tempImagePath { get; set; }

        // 实现 ITreeNode 接口
        string ITreeNode.Name
        {
            get => FileName;
            set => FileName = value;
        }

        // 实现展开状态（图片节点不需要展开，但为了接口一致性）
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        // 实现 IsSelected（用于 TreeView 导航）
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        [ObservableProperty]
        private SolidColorBrush colorBrush = Brushes.Transparent;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private DamageStatus _status;

        public Brush StatusColor
        {
            get
            {
                return this.Status switch
                {
                    DamageStatus.HASDAMAGE => Brushes.LightCoral,
                    DamageStatus.NODAMAGE => Brushes.LightGreen,
                    DamageStatus.KNOWN => Brushes.Yellow,
                    _ => Brushes.Transparent
                };
            }
        }
    }
    // 在 DamageMaker.Models 命名空间中添加
    public class ImageGroupTree : ITreeNode, INotifyPropertyChanged
    {
        public string FileName { get; set; }

        // ITreeNode 接口实现
        public string Name
        {
            get => FileName;
            set => FileName = value;
        }

        public List<ImageDamageCategory> DamageCategories { get; set; }
        public int DamageCount => DamageCategories?.Count ?? 0;

        // 展开状态
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        // 选中状态
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // 多选状态
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    // 同步子节点状态
                    if (DamageCategories != null)
                    {
                        foreach (var category in DamageCategories)
                        {
                            category.IsChecked = value;
                        }
                    }
                }
            }
        }

        public SolidColorBrush ColorBrush { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // 专门用于图片模式的伤损类别类
    public class ImageDamageCategory : ITreeNode, INotifyPropertyChanged
    {
        public string Name { get; set; }
        public SolidColorBrush ColorBrush { get; set; }
        public int? Count { get; set; }
        public List<ImageDamageDetail> Children { get; set; }

        // 展开状态
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        // 选中状态
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // 多选状态
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    // 同步子节点状态
                    if (Children != null)
                    {
                        foreach (var child in Children)
                        {
                            child.IsChecked = value;
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // 专门用于图片模式的伤损详情类
    // 修改 ImageDamageDetail 类，添加原图路径信息
    public partial class ImageDamageDetail : ObservableObject, ITreeNode
    {
        public string FileName { get; set; }
        public int Count { get; set; }
        public float? weight { get; set; }

        [ObservableProperty]
        private int _displayIndex;

        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private SolidColorBrush colorBrush = Brushes.Transparent;

        [ObservableProperty]
        private DamageStatus _status;

        // 实现 ITreeNode 接口
        string ITreeNode.Name
        {
            get => FileName;
            set => FileName = value;
        }

        // 实现展开状态
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        // 实现 IsSelected
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}