using Nytherion.Core.Managers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class VillageInstaller : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<StageManager>();
        builder.RegisterComponentInHierarchy<SceneTransitionManager>();
        builder.RegisterComponentInHierarchy<InputManager>();
        builder.RegisterComponentInHierarchy<EventManager>();
        builder.RegisterComponentInHierarchy<InteractionManager>();
    }
}