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
        private int serialNumber;//序号
        public string FolderName { get; set;}
        public DateTime CreatTime { get; set;}
        public bool HasDamage { get; set;}
        public int PngCount { get; set;}
        public int DamagePngCount { get; set;}

        public bool HasDocx { get; set;}
        public bool HasMileage { get; set;}
        public bool HasFolderInfo { get; set;}
        public bool IsReadOnly { get=>!HasFolderInfo;}

        [ObservableProperty]
        private string remark = string.Empty;



        partial void OnRemarkChanged(string? oldValue, string newValue)
        {
            try
            {
                Console.WriteLine("正在保存备注到数据库...");
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
