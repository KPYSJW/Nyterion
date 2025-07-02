using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class EngravingGridState
    {
        [Serializable]
        public class SavedEngravingBlock
        {
            public string engravingId;
            public int gridRow;
            public int gridCol;
            public List<Vector2Int> shape;
            public int rotationState;
        }

        public List<SavedEngravingBlock> placedBlocks = new List<SavedEngravingBlock>();
    }
}