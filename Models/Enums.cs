using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.Models
{

    public enum DamageCategory
    {
        接头 = 0,
        零度迟到波 = 1,
        普通焊缝 = 2,
        七十度螺孔回波 = 3,
        轨形变换 = 4,
        其他核伤 = 5,
        焊缝核伤 = 6,
        螺孔裂纹 = 7,
        零度异常 = 8,
        轨腰裂纹 = 9,
        轨底裂纹_规则 = 10,
        加固焊缝 = 11,
        断面 = 12,
        轨面剥离 = 13,
        焊缝标记 = 14,
        无伤标记 = 15,
        焊缝且无伤标记 = 16,
        轻伤标记 = 17,
        轻伤发展 = 18,
        重伤标志 = 19,
        焊缝且无焊缝标记 = 21,
        假像波 = 22,
        鱼鳞伤_规则 = 23,
        岔心 = 24,
        核伤3_母材 = 26,
        核伤1_母材 = 27,
        正常螺孔 = 28,
        水平裂纹 = 29,
        核伤2_母材 = 30,
        核伤2_非母材 = 31,
        水平裂纹_母材 = 32,
        斜裂纹_母材 = 33,
        月牙伤2 = 34,
        核伤4_母材 = 35,
        鱼鳞伤 = 36,
        轨底裂纹 = 37,
        轨面剥离_母材 = 38,
        接头出波不全 = 39,
        水平裂纹_非母材 = 40,
        轨面剥离_非母材 = 41,
        核伤1_非母材 = 42,
        核伤3_非母材 = 43,
        核伤4_非母材 = 44,
        岔心_非母材 = 45,
        厂焊=46,
        铝热焊=47,
        超速=48,
        倒车=49,
        失底波 = 51,
        有焊缝标记核伤 = 52
    }
    public enum MoveDirection
    {
        Left,
        Right
    }
    public enum DamageStatus
    {
        UNKNOWN,
        KNOWN,
        HASDAMAGE,
        NODAMAGE,
    }
}
