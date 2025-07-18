using System;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class StatModifier
    {
        public StatType stat;
        public float value;
    }
}