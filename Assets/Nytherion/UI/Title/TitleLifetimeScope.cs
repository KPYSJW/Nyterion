using UnityEngine;
using Nytherion.UI.Title;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Title
{
    public class TitleLifetimeScope : LifetimeScope
    {
        [Header("Title UI Components")]
        [SerializeField] private TitleMenuManager titleMenuManagerPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            var existingTitleMenuManager = FindObjectOfType<TitleMenuManager>();
            if (existingTitleMenuManager != null)
            {
                builder.RegisterComponent(existingTitleMenuManager)
                    .AsSelf();
            }
            else if (titleMenuManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(titleMenuManagerPrefab, Lifetime.Scoped)
                    .AsSelf();
            }
            else
            {
                Debug.LogError("[TitleLifetimeScope] TitleMenuManager not found in scene and no prefab assigned!");
            }
        }
    }
}