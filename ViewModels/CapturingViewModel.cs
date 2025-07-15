using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamageMaker.FileHandle;
using DamageMaker.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.ViewModels
{
    internal partial class CapturingViewModel : ObservableObject
    {
        [ObservableProperty]
        bool isAnalysising;

        [RelayCommand]
        internal void ImgsAnalysis()
        {
           FilesDetection.DetectNewFiles(IsAnalysising,Settings.Default.TrackData);
        }
    }
}
