using UnityEngine;
using VContainer.Unity;

[System.Serializable]
public class VContainerBootstrap : MonoBehaviour
{
    [SerializeField] private RootLifetimeScope rootLifetimeScope;

    private void Awake()
    {
        // VContainer Root 설정
        if (rootLifetimeScope != null)
        {
            DontDestroyOnLoad(rootLifetimeScope.gameObject);
        }
    }
}