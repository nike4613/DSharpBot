using System;
using System.Collections.Generic;
using System.Diagnostics;
using DSharpBotCore.Extensions;

namespace DSharpBotCore.Modules.Modes.Genesys
{
    class Dice
    {
        public class SizeAttribute : Attribute
        {
            public readonly int Size;

            public SizeAttribute(int size)
            {
                Size = size;
            }
        }

        public class FacesAttribute : Attribute
        {
            public IReadOnlyList<(Symbol first, Symbol second)> Faces => faces;
            private readonly List<(Symbol first, Symbol second)> faces = new List<(Symbol first, Symbol second)>();

            public FacesAttribute(params Symbol[] faceParts)
            {
                for (int i = 0; i < faceParts.Length; i+=2)
                    faces.Add((faceParts[i], faceParts[i+1]));
            }
        }

        public enum DiceType
        {
            [Size(6)]
            [Faces(Symbol.None, Symbol.None, 
                Symbol.None, Symbol.None, 
                Symbol.Success, Symbol.None, 
                Symbol.Success, Symbol.Advantage,
                Symbol.Advantage, Symbol.Advantage,
                Symbol.Advantage, Symbol.None)]
            Boost = 0,
            [Size(6)]
            [Faces(Symbol.None, Symbol.None,
                Symbol.None, Symbol.None,
                Symbol.Failure, Symbol.None,
                Symbol.Failure, Symbol.None,
                Symbol.Threat, Symbol.None,
                Symbol.Threat, Symbol.None)]
            Setback = 1,
            [Size(8)]
            [Faces(Symbol.None, Symbol.None,
                Symbol.Success, Symbol.None,
                Symbol.Success, Symbol.None,
                Symbol.Success, Symbol.Success,
                Symbol.Advantage, Symbol.None,
                Symbol.Advantage, Symbol.None,
                Symbol.Success, Symbol.Advantage,
                Symbol.Advantage, Symbol.Advantage)]
            Ability = 2,
            [Size(8)]
            [Faces(Symbol.None, Symbol.None,
                Symbol.Failure, Symbol.None,
                Symbol.Failure, Symbol.Failure,
                Symbol.Threat, Symbol.None,
                Symbol.Threat, Symbol.None,
                Symbol.Threat, Symbol.None,
                Symbol.Threat, Symbol.Threat,
                Symbol.Failure, Symbol.Threat)]
            Difficulty = 3,
            [Size(12)]
            [Faces(Symbol.None, Symbol.None,
                Symbol.Success, Symbol.None,
                Symbol.Success, Symbol.None,
                Symbol.Success, Symbol.Success,
                Symbol.Success, Symbol.Success,
                Symbol.Advantage, Symbol.None,
                Symbol.Success, Symbol.Advantage,
                Symbol.Success, Symbol.Advantage,
                Symbol.Success, Symbol.Advantage,
                Symbol.Advantage, Symbol.Advantage,
                Symbol.Advantage, Symbol.Advantage,
                Symbol.Triumph, Symbol.None)]
            Proficiency = 4,
            [Size(12)]
            [Faces(Symbol.None, Symbol.None,
                Symbol.Failure, Symbol.None,
                Symbol.Failure, Symbol.None,
                Symbol.Failure, Symbol.Failure,
                Symbol.Failure, Symbol.Failure,
                Symbol.Threat, Symbol.None,
                Symbol.Threat, Symbol.None,
                Symbol.Failure, Symbol.Threat,
                Symbol.Failure, Symbol.Threat,
                Symbol.Threat, Symbol.Threat,
                Symbol.Threat, Symbol.Threat,
                Symbol.Despair, Symbol.None)]
            Challenge = 5 
        }

        public static (Symbol first, Symbol second) Roll(Random rand, DiceType type)
        {
            var size = type.GetAttribute<SizeAttribute>().Size;
            var options = type.GetAttribute<FacesAttribute>().Faces;

            Debug.Assert(size == options.Count);

            return options[rand.Next(0, size)];
        }
    }
}
