using Nytherion.Core.Managers;
using UnityEngine;
using Zenject;

public class VillageInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<StageManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<SceneTransitionManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<InputManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<EventManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<InteractionManager>().FromComponentInHierarchy().AsSingle();
    }
}