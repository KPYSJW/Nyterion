using System;

namespace Nytherion.Core.Enums
{
    [Serializable]
    public enum PuzzleColor
    {
        Red,
        Blue,
        Yellow,
        Green,
        Orange,
        Purple
    }

    [Serializable]
    public enum PuzzleState
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }
}