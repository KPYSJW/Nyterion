using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data;

namespace Nytherion.Core
{
    public class ObjectPoolManager : MonoBehaviour
    {
        public static ObjectPoolManager Instance { get; private set; }

        [System.Serializable]
        public class Pool
        {
            public string tag;
            public GameObject prefab;
            public int size;
        }
        public List<Pool> pools;
        public Dictionary<string, Queue<GameObject>> poolDictionary;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            poolDictionary = new Dictionary<string, Queue<GameObject>>();
            foreach (Pool pool in pools)
            {
                if (pool.prefab == null)
                {
                    Debug.LogWarning($"Pool with tag '{pool.tag}' has a null prefab. Skipping this pool.");
                    continue; 
                }
                Queue<GameObject> objectPool = new Queue<GameObject>();
                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = Instantiate(pool.prefab);
                    obj.SetActive(false);
                    objectPool.Enqueue(obj);
                }
                poolDictionary.Add(pool.tag, objectPool);
            }
        }
        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"Pool with tag '{tag}' doesn't exist.");
                return null;
            }

            if (poolDictionary[tag].Count == 0)
            {
                Debug.LogWarning($"Pool with tag '{tag}' is empty. Consider increasing pool size or implementing dynamic expansion.");
                return null; // Or handle by instantiating a new object if dynamic expansion is desired
            }

            GameObject obj = poolDictionary[tag].Dequeue();
            obj.SetActive(true);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            // poolDictionary[tag].Enqueue(obj); // Object should not be immediately returned
            return obj;
        }

        public void ReturnToPool(string tag, GameObject objectToReturn)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"Pool with tag '{tag}' does not exist. Destroying object instead.");
                Destroy(objectToReturn);
                return;
            }

            objectToReturn.SetActive(false);
            // Optionally, reset object's state here (e.g., position, parent, components)
            poolDictionary[tag].Enqueue(objectToReturn);
        }
    }
}
