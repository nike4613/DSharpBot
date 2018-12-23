using System;

namespace DSharpBotCore.Modules.Modes.Genesys
{
    public class PositiveAttribute : Attribute { }
    public class NegativeAttribute : Attribute { }

    public class CanceledByAttribute : Attribute
    {
        public readonly Symbol[] Cancels;
        public bool MaintainsEffects;

        public CanceledByAttribute(params Symbol[] others)
        {
            Cancels = others;
        }
    }

    public enum Symbol
    {
        None = -1,

        [Positive, CanceledBy(Failure, Despair)]
        Success = 0,
        [Positive, CanceledBy(Threat)]
        Advantage = 1,
        [Positive, CanceledBy(Failure, Despair, MaintainsEffects = true)]
        Triumph = 2,

        [Negative, CanceledBy(Success, Triumph)]
        Failure = 3,
        [Negative, CanceledBy(Advantage)]
        Threat = 4,
        [Negative, CanceledBy(Success, Triumph, MaintainsEffects = true)]
        Despair = 5
    }
}
