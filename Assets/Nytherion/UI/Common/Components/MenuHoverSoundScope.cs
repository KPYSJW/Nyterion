using UnityEngine;

namespace Nytherion.UI.Components
{
    /// <summary>타이틀·일시 정지 메뉴와 그 하위 설정에서만 호버 효과음을 허용한다.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuHoverSoundScope : MonoBehaviour
    {
        public static void Mark(GameObject root)
        {
            if (root != null && root.GetComponent<MenuHoverSoundScope>() == null)
                root.AddComponent<MenuHoverSoundScope>();
        }
    }
}
