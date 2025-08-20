using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.Message
{
    class ConcealFishScaleMessage
    {
        public bool IsConcealFishScale { get; }
        public ConcealFishScaleMessage(bool isConcealFishScale)
        {
            IsConcealFishScale = isConcealFishScale;
        }
    }
}
