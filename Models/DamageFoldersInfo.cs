using CommunityToolkit.Mvvm.ComponentModel;
using DamageMaker.SqliteServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DamageMaker.Properties;
namespace DamageMaker.Models
{
    public partial class DamageFoldersInfo:ObservableObject
    {
        [ObservableProperty]
        private int _serialNumber; // 序号

        [ObservableProperty]
        private string _folderName = string.Empty;

        [ObservableProperty]
        private DateTime _creatTime;

        [ObservableProperty]
        private bool _hasDamage;

        [ObservableProperty]
        private int _pngCount;

        [ObservableProperty]
        private int _damagePngCount;

        [ObservableProperty]
        private bool _hasDocx;

        [ObservableProperty]
        private string _instruments = string.Empty;

        [ObservableProperty]
        private int _suspectedDamageCount;

        [ObservableProperty]
        private string _remark = string.Empty;

        [ObservableProperty]
        private string _selectedLineType;

        [ObservableProperty]
        private string _selectedUpOrDown;

        [ObservableProperty]
        private int _cycleNumber;

        [ObservableProperty]
        private string _selectedRailType;

        [ObservableProperty]
        private string _serialNumber1;

        [ObservableProperty]
        private DateTime? _lastOpenTime;

        partial void OnRemarkChanged(string? oldValue, string newValue)
        {
            try
            {
                var sqlHelper = new SQLHelper(Settings.Default.SqlPath);
              
                        sqlHelper.UpdateFolderRemark(FolderName,newValue);
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Error("保存备注失败：" + ex.Message);
            }
        }



        public override string ToString()
        {
            return FolderName;
        }
    


    }

}
