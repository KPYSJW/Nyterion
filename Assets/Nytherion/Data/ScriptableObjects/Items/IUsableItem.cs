using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{
    /// <summary>
    /// 사용 가능한 아이템을 나타내는 인터페이스
    /// </summary>
    public interface IUsableItem
    {
        /// <summary>
        /// 아이템을 사용합니다.
        /// </summary>
        void Use();
        
        /// <summary>
        /// 아이템 사용을 취소합니다. (선택사항)
        /// </summary>
        void CancelUse();
    }
}
