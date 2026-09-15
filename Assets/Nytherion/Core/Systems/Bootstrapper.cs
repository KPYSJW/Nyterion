using UnityEngine;
using Nytherion.Core.Data;

namespace Nytherion.Core.Systems
{
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Execute()
        {
            UserSettings.ApplyBeforeSceneLoad();

            if (Object.FindObjectOfType<RootLifetimeScope>() != null) return;

            
            GameObject bootPrefab = Resources.Load<GameObject>("BootSystem");

            if (bootPrefab != null)
            {
                Object.Instantiate(bootPrefab);
            }
            
        }
    }
}
