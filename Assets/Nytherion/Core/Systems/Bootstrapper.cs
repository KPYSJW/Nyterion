using UnityEngine;

namespace Nytherion.Core.Systems
{
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Execute()
        {
            if (Object.FindObjectOfType<RootLifetimeScope>() != null) return;

            
            GameObject bootPrefab = Resources.Load<GameObject>("BootSystem");

            if (bootPrefab != null)
            {
                Object.Instantiate(bootPrefab);
                Debug.Log("[Bootstrapper] 씬 우회 감지! 필수 시스템(BootSystem)을 자동으로 소환했습니다.");
            }
            else
            {
                Debug.LogWarning(" [Bootstrapper] Resources 폴더에서 'BootSystem' 프리팹을 찾을 수 없습니다! 이름을 확인해주세요.");
            }
        }
    }
}