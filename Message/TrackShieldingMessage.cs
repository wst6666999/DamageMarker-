using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamageMaker.Message
{
    public sealed record TrackShieldingMessage(
        int BeforeCount,
        int AfterCount
    );
}
