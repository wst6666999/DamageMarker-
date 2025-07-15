using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;


namespace DamageMaker.Models
{
    public class Records
    {
        public record OcrData(string ImgFullPath, string MileageText);
        public record DamageCategoryRecord(int Id, string CategoryName, SolidColorBrush CategoryColor,string? Remark,bool IsRule);
        public static List<DamageCategoryRecord> DamageCategoryData = new()
        {
            new (23, "鱼鳞伤(规则)", Brushes.Red, "鱼鳞伤：在轨头区域找较长的鱼鳞伤（6个点以上）", true),
            new (27, "核伤1(母材)", Brushes.Red, "核伤1：在轨头区域找冒尖的，纵坐标最长（3个点以上）", true),
            new (30, "核伤2(母材)", Brushes.Red, "核伤2：在一次波区域（2个点以上）", true),
            new (26, "核伤3(母材)", Brushes.Red, "核伤3:在轨头区域找零星单个的（3个点以上）", true),
            new (35, "核伤4(母材)", Brushes.Red, "多通道出波", true),
            new (38, "轨面剥离(母材)", Brushes.Red, "剥离:轨头三个角度探头都有出波。（内70°，外70°、直70°）", true),
            new (32, "水平裂纹(母材)", Brushes.Red, null, true),
            new (33, "斜裂纹(母材)", Brushes.Red, "单边37度,并且轨底失波", true),
            new (10, "轨底裂纹(规则)", Brushes.Red, "轨底裂纹：找轨底正八字", true),
            new (34, "月牙伤2", Brushes.Red, "焊缝底下出现的单边八字", true),
            new (40, "水平裂纹(非母材)", Brushes.Red, null, true),
            new (41, "轨面剥离(非母材)", Brushes.Red, null, true),
            new (42, "核伤1(非母材)", Brushes.Red, null, true),
            new (31, "核伤2(非母材)", Brushes.Red, "核伤2：在一次波区域（2个点以上）", true),
            new (43, "核伤3(非母材)", Brushes.Red, null, true),
            new (44, "核伤4(非母材)", Brushes.Red, null, true),
            new (45, "岔心(非母材)", Brushes.Red, null, true),
            new (0, "接头", Brushes.Green, null, false),
            new (1, "零度迟到波", Brushes.Green, null, false),
            new (2, "普通焊缝", Brushes.Green, null, false),
            new (3, "七十度螺孔回波", Brushes.Green, null, false),
            new (4, "轨形变换", Brushes.Green, null, false),
            new (5, "母材核伤", Brushes.Red, null, false),
            new (6, "焊缝核伤", Brushes.Red, null, false),
            new (7, "螺孔裂纹", Brushes.Red, null, false),
            new (8, "零度异常", Brushes.Red, null, false),
            new (9, "轨腰裂纹", Brushes.Red, null, false),
            new (11, "加固焊缝", Brushes.Green, null, false),
            new (12, "断面", Brushes.Green, null, false),
            new (13, "轨面剥离", Brushes.Red, null, false),
            new (14, "焊缝标记", Brushes.YellowGreen, null, false),
            new (15, "无伤标记", Brushes.YellowGreen, null, false),
            new (16, "焊缝且无伤标记", Brushes.YellowGreen, null, false),
            new (17, "轻伤标记", Brushes.YellowGreen, null, false),
            new (18, "轻伤发展", Brushes.YellowGreen, null, false),
            new (19, "重伤标志", Brushes.YellowGreen, null, false),
          //  new (20, "作业违规", Brushes.YellowGreen, null, false),
            new (21, "焊缝且无焊缝标记", Brushes.YellowGreen, null, false),
            new (22, "假像波", Brushes.Green, null, false),
            new (24, "岔心", Brushes.Green, null, false),
            //new (25, "核伤2(母材)", Brushes.Red, "核伤2：在一次波区域（2个点以上）", false),
            new (28, "正常螺孔", Brushes.Green, null, false),
            new (29, "水平裂纹", Brushes.Red, null, false),
            new (36, "鱼鳞伤", Brushes.Red, null, false),
            new (37, "轨底裂纹", Brushes.Red, null, false),
            new (39, "接头出波不全", Brushes.YellowGreen, null, false),
            new (46, "厂焊",Brushes.YellowGreen,null,false),
            new (47, "铝热焊",Brushes.YellowGreen,null,false),
        };

    }

    }

