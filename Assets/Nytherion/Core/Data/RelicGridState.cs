using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class RelicGridState
    {
        [Serializable]
        public class SavedRelicBlock
        {
            public string relicId;
            public int gridRow;
            public int gridCol;
            public List<Vector2Int> shape;
            public int rotationState;
        }

        public List<SavedRelicBlock> placedBlocks = new List<SavedRelicBlock>();
    }
}