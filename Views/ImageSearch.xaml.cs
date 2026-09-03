using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DamageMaker.ViewModels;

namespace DamageMaker.Views
{
    /// <summary>
    /// ImageSearch.xaml 的交互逻辑
    /// </summary>
    public partial class ImageSearch : Window
    {
        public ImageSearch()
        {
            InitializeComponent();
            DataContext = new ImageSearchViewModel();
        }

        private void imageDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void imageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
