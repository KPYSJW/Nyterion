using UnityEngine;
using Nytherion.UI.Title;
using VContainer;
using VContainer.Unity;
using Nytherion.UI.Controllers;

namespace Nytherion.UI.Title
{
    public class TitleLifetimeScope : LifetimeScope
    {
        [Header("Title UI Components")]
        [SerializeField] private TitleMenuManager titleMenuManagerPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            LanguageDropdownController languageDropdown = FindObjectOfType<LanguageDropdownController>(true);
            if (languageDropdown != null)
            {
                builder.RegisterComponent(languageDropdown);
            }

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
