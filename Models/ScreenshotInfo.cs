using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.Models
{
    public class ScreenshotInfo
    {
        public int ScreenshotOffset { get; set; }

        public int ScreenshotWidthPx { get; set; }
        public MoveDirection ScreenshotDirection { get; set; }

        public bool isAnalyzed { get; set; }
        public RailInfo RailWayInfo { get; set; }
    }
    public class RailInfo
    {
        public string? RailWayName { get; set; }
        public string? Instruments { get; set; }
        public string SerialNumber { get; set;}
        public string WorkDate { get; set; }
        public string WorkSection { get; set; }
        public string? WorkLength { get; set; }
        public string? SelectedLineType { get; set; }

        public string? SelectedRailType { get; set; }
        public string? WorkGroup { get; set; }
        public string? OperatorName { get; set; }
        public string? AnalyzeTime { get; set; }

        public string? StartMileage { get; set; }

        public string? EndMileage { get; set; }
        public string? ElapsedTimeForScrrnshot { get; set; }

        public string? ElapsedTimeForAnalyze { get; set; }
        public string? Remark { get; set; }
        public string? SelectedRouteLine { get; set; }
        public string? SelectedUpOrDown { get; set; }
        public int? CycleNumber { get; set; }
    }

}
