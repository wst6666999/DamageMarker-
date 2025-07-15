using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DamageMaker.ViewModels
{
    internal partial class NonClientViewModel : ObservableObject
    {
        [RelayCommand]
        private void ToggleTheme(SkinType theme) //切换主题
        {
            ResourceDictionary Resources = Application.Current.Resources;
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri(
                        $"pack://application:,,,/HandyControl;component/Themes/Skin{theme.ToString()}.xaml"
                    )
                }
            );
            Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/HandyControl;component/Themes/Theme.xaml"
                    )
                }
            );
        }
    }
}
