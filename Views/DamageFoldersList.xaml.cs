using DamageMaker.Models;
using DamageMaker.ViewModels;
using DamageMarker.Views;
using HandyControl.Controls;
using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using DamageMaker.SqliteServer;


namespace DamageMaker.Views
{
    /// <summary>
    /// DamageFoldersList.xaml 的交互逻辑
    /// </summary>
    public partial class DamageFoldersList : System.Windows.Window
    {
       

        public DamageFoldersList()
        {
            InitializeComponent();
            DataContext = new DamageFoldersListViewModel();
            DamageFoldersListViewModel.OpenedDamageFolder += OnOpenedDamageFolder;
        }

        private void OnOpenedDamageFolder(string folderPath)
        {

            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var aa = folderList.SelectedItems;
            foreach (var item in folderList.SelectedItems)
            {
                Console.WriteLine(item);
            }
        }

        private void folderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = DataContext as DamageFoldersListViewModel;
            if (viewModel != null)
            {
                viewModel.SelectedFolders = new ObservableCollection<DamageFoldersInfo>(folderList.SelectedItems.Cast<DamageFoldersInfo>());
              
            }
        }

      

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        

    }

}
