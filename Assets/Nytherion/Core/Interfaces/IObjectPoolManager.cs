using UnityEngine;

public interface IObjectPoolManager
{
    void Prewarm(GameObject prefab, int count, Transform parent = null);

    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

    void Despawn(GameObject instance);

    void ClearPool(GameObject prefab);
}