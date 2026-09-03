using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMarker.Views;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using System;

namespace DamageMaker.ViewModels
{
    public partial class MileageInputDialogViewModel : ObservableObject, IDialogResultable<string>
    {
        [ObservableProperty]
        private string mileage;

        public string Result { get; set; }
        public Action CloseAction { get; set; }

      

        [RelayCommand]
        private void Confirm()
        {
            Result = Mileage;
            CloseAction?.Invoke();
            MainWindow.MainVm.LoadProjectOverview();
        }

        [RelayCommand]
        private void Cancel()
        {
            Result = null;
            CloseAction?.Invoke();
            MainWindow.MainVm.LoadProjectOverview();
        }
    }
}