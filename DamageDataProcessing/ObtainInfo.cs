using DamageMaker.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.DamageDataProcessing
{
    public static class ObtainInfo
    {
       public static List<KeyValuePair<int, float>> GetCategoryAndCount(
            [NotNull] List<DamageData> damageDataListPara,bool ShowNotDamage=false)
        {
            List<float> damageCategory = new List<float>();
            foreach (var d in damageDataListPara)
            {
                var a = GetColumn4(d.DamagePoint);
                damageCategory.AddRange(a);
            }
            var result = damageCategory
                .GroupBy(x => x)
                .OrderByDescending(x => x.Count())
                .Select(g => new KeyValuePair<int, float>(g.Count(), g.Key))
                .ToList();

            Records.DamageCategoryData.ForEach(x =>
            {
                if (result.All(y => y.Value != x.Id) && ShowNotDamage)
                {
                    result.Add(new KeyValuePair<int, float>(0, x.Id));
                }
            });
            return result;
        }

       



        public static float[] GetColumn4(float[][] damageDatas)
        {
            return damageDatas.Select(x => x[4]).ToArray();
        }


    }
}
