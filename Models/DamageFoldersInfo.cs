using CommunityToolkit.Mvvm.ComponentModel;
using DamageMaker.SqliteServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public  string Remark {get; set;} = string.Empty;
       
        public override string ToString()
        {
            return FolderName;
        }
    
    }
}
