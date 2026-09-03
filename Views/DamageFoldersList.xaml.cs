using DamageMaker.Models;
using DamageMaker.ViewModels;
using DamageMarker.Views;
using HandyControl.Controls;
using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using System.Windows.Threading;
using DamageMaker.SqliteServer;

namespace DamageMaker.Views
{
    /// <summary>
    /// DamageFoldersList.xaml 的交互逻辑
    /// </summary>
    public partial class DamageFoldersList : System.Windows.Window
    {
        private DamageFoldersListViewModel? _viewModel;
        private bool _isRestoringSelection;

        public DamageFoldersList()
        {
            InitializeComponent();

            _viewModel = new DamageFoldersListViewModel();
            DataContext = _viewModel;

            _viewModel.DamageFolders.CollectionChanged += DamageFolders_CollectionChanged;
            DamageFoldersListViewModel.OpenedDamageFolder += OnOpenedDamageFolder;

            // 防止窗口反复打开后，静态事件重复订阅导致重复触发或内存泄漏
            Closed += DamageFoldersList_Closed;
        }

        private void DamageFoldersList_Closed(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.DamageFolders.CollectionChanged -= DamageFolders_CollectionChanged;
            }

            DamageFoldersListViewModel.OpenedDamageFolder -= OnOpenedDamageFolder;
            Closed -= DamageFoldersList_Closed;
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
            if (_isRestoringSelection) return;

            if (DataContext is DamageFoldersListViewModel viewModel)
            {
                // 翻页或刷新当前页时，DataGrid 会因为 ItemsSource 清空而触发 RemovedItems。
                // 这类 RemovedItems 不能当作用户取消勾选，否则跨页选择会丢失。
                if (viewModel.IsChangingPage) return;

                var addedItems = e.AddedItems.Cast<DamageFoldersInfo>().ToList();
                var removedItems = e.RemovedItems.Cast<DamageFoldersInfo>().ToList();

                viewModel.UpdateCrossPageSelection(addedItems, removedItems);
            }
        }

        private void DamageFolders_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 等当前页数据渲染完成后，再把之前跨页勾选过的行重新选中。
            Dispatcher.BeginInvoke(new Action(RestoreSelectionsOnCurrentPage), DispatcherPriority.Loaded);
        }

        private void RestoreSelectionsOnCurrentPage()
        {
            if (_viewModel == null) return;

            _isRestoringSelection = true;
            try
            {
                folderList.SelectedItems.Clear();

                foreach (var item in folderList.Items.OfType<DamageFoldersInfo>())
                {
                    if (_viewModel.IsFolderGloballySelected(item))
                    {
                        folderList.SelectedItems.Add(item);
                    }
                }

                DamageFoldersInfo? itemToScroll = null;

                if (!string.IsNullOrWhiteSpace(_viewModel.FolderNameToScrollIntoView))
                {
                    itemToScroll = folderList.Items
                        .OfType<DamageFoldersInfo>()
                        .FirstOrDefault(item => string.Equals(
                            item.FolderName,
                            _viewModel.FolderNameToScrollIntoView,
                            StringComparison.OrdinalIgnoreCase));
                }

                itemToScroll ??= folderList.SelectedItem as DamageFoldersInfo;

                if (itemToScroll != null)
                {
                    folderList.SelectedItem = itemToScroll;
                    folderList.ScrollIntoView(itemToScroll);
                }
            }
            finally
            {
                _isRestoringSelection = false;
            }
        }

        private void SelectedFoldersMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || _viewModel.SelectedFolders.Count == 0)
            {
                HandyControl.Controls.MessageBox.Info("当前没有已勾选的数据");
                return;
            }

            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void SelectedFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is DamageFoldersInfo folder)
            {
                if (_viewModel.JumpToSelectedFolderCommand.CanExecute(folder))
                {
                    _viewModel.JumpToSelectedFolderCommand.Execute(folder);
                }
            }
        }

        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
