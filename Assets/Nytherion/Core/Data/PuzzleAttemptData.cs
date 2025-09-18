using System;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class PuzzleAttemptData
    {
        public string puzzleId;
        public PuzzleType puzzleType;
        public int remainingAttempts;
        public int maxAttempts;
        public bool isCompleted;
        public bool isFailed;
        public long lastAttemptTimestamp;

        public PuzzleAttemptData(string id, PuzzleType type, int maxAttempts = 3)
        {
            this.puzzleId = id;
            this.puzzleType = type;
            this.maxAttempts = maxAttempts;
            this.remainingAttempts = maxAttempts;
            this.isCompleted = false;
            this.isFailed = false;
            this.lastAttemptTimestamp = 0;
        }

        public bool CanAttempt()
        {
            return !isCompleted && !isFailed && remainingAttempts > 0;
        }

        public void UseAttempt()
        {
            if (remainingAttempts > 0)
            {
                remainingAttempts--;
                lastAttemptTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (remainingAttempts <= 0)
                {
                    isFailed = true;
                }
            }
        }

        public void CompleteSuccess()
        {
            isCompleted = true;
            lastAttemptTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void Reset()
        {
            remainingAttempts = maxAttempts;
            isCompleted = false;
            isFailed = false;
            lastAttemptTimestamp = 0;
        }
    }
}