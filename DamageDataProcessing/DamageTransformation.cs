using DamageMaker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DamageMaker.DamageDataProcessing
{
   public static class DamageTransformation
    {
        //临近伤损转化

        /// <summary>
        /// 将焊缝附近的伤损点转化为焊缝附近伤损点   
        /// </summary>
        /// <param name="data">伤损数据</param>
        /// <param name="AllImgCount">所有捕获图片的数量</param>
        /// <returns>返回焊缝附近的伤损数据</returns>
        public static List<DamageData> NearWeldTransformation(List<DamageData> data,int AllImgCount)
        {
            var newDamageDataList = data.Select(d => new DamageData
            {
                Url = d.Url,
                DamagePoint = d.DamagePoint.ToArray()
            }).ToList();


            ///<summary>
            ///14:焊缝
            ///0:接头
            ///2:普通焊缝
            ///21:焊缝且无伤标记
            ///16:焊缝且无焊缝标记
            /// </summary>
            for (int i = 0; i < newDamageDataList.Count; i++)
            {
                var hasWeldAndJoint = newDamageDataList[i].DamagePoint.Any(x => x[4] == 14 || x[4] == 0 ||  x[4]==39||x[4] == 24);
                if (hasWeldAndJoint)
                {
                    var Name = Path.GetFileNameWithoutExtension(newDamageDataList[i].Url);
                    int serial;
                    if (Name.Contains("_"))
                    {
                        //获取_后面的数字
                        var serialString = Name.Substring(Name.LastIndexOf("_") + 1);
                        serial = int.Parse(serialString);
                    }
                    else
                    {
                        serial = int.Parse(Name);
                    }

                        //这张图片的伤损点转化
                        newDamageDataList[i].DamagePoint = newDamageDataList[i].DamagePoint.Select(x =>
                        {
                            //调用BaseMetalTransform方法实现转化
                            BaseMetalTransform(x);
                            return x;
                        }).ToArray();
 
                    //前一张图片的伤损点转化
                    if (serial - 1 > -1)
                    {
                        newDamageDataList = newDamageDataList.Select(x =>
                        {
                            //使用正则表达式检查当前元素的 `Url` 是否包含前一张图片的序号
                            if (Regex.IsMatch(Path.GetFileNameWithoutExtension(x.Url), $@"(^|_)({serial - 1})$"))
                            {
                                    x.DamagePoint = x.DamagePoint.Select(x =>
                                    {
                                        BaseMetalTransform(x);
                                        return x;
                                    }).ToArray();
                            }
                            return x;
                        }).ToList();
                    }

                    //下一张图片的伤损点转化
                    if (serial + 1 < AllImgCount)
                    {
                        newDamageDataList = newDamageDataList.Select(x =>
                        {
                            if (Regex.IsMatch(Path.GetFileNameWithoutExtension(x.Url), $@"(^|_)({serial + 1})$"))
                            {
                                    x.DamagePoint = x.DamagePoint.Select(x =>
                                    {
                                        BaseMetalTransform(x);
                                        return x;
                                    }).ToArray();
                               // }
                            }
                            return x;
                        }).ToList();
                    }
                }
            }
            // 返回新的 DamageDataList
            return newDamageDataList;

        }

        /// <summary>
        /// 将焊缝附近的伤损点转化为焊缝附近伤损点
        /// </summary>
        /// <param name="x">返回_对应的伤损标识</param>
        private static void BaseMetalTransform(float[] x)
        {         
            x[4] = x[4] switch
            {
                30 => 31,
                32 => 40,
                38 => 41,
                27 => 42,
                26 => 43,
                35 => 44,                            
                _ => x[4]
            };
        }
    }
}
