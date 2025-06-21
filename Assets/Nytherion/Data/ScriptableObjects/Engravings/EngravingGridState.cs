using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Engravings
{
    [Serializable]
    public class EngravingGridState
    {
        // 저장될 개별 각인 블록의 정보를 담는 내부 클래스
        [Serializable]
        public class SavedEngravingBlock
        {
            public string engravingId;      // 각인 데이터의 이름 또는 고유 ID
            public int gridRow;             // 그리드 상의 행 위치
            public int gridCol;             // 그리드 상의 열 위치
            public List<Vector2Int> shape;  // 회전 상태를 포함한 현재 모양

        }

        public List<SavedEngravingBlock> placedBlocks = new List<SavedEngravingBlock>();
    }
}