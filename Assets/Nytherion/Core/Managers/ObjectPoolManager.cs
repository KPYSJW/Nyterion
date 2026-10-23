using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Data;

namespace Nytherion.Core.Managers
{
    public class ObjectPoolManager : BaseManager
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

        private Dictionary<string, Transform> poolRoots;
        private Dictionary<string, Transform> categoryRoots;
        private Dictionary<GameObject, string> instancePoolTags;
        private Dictionary<string, Pool> poolConfigurations;

        private IObjectResolver container;

        [Inject]
        public void Construct(IObjectResolver container)
        {
            this.container = container;
        }

        protected override void Awake()
        {
            // Resolve 중에는 UI 의존성이 있는 풀 오브젝트를 생성하지 않는다.
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (Instance == this && autoInitializeOnAwake)
            {
                Initialize();
            }
        }

        protected override void OnInitializeInternal()
        {
            poolDictionary = new Dictionary<string, Queue<GameObject>>();
            poolRoots = new Dictionary<string, Transform>();
            categoryRoots = new Dictionary<string, Transform>();
            instancePoolTags = new Dictionary<GameObject, string>();
            poolConfigurations = new Dictionary<string, Pool>();

            if (pools == null)
            {
                pools = new List<Pool>();
            }

            foreach (Pool pool in pools)
            {
                if (pool.prefab == null) continue;
                CreateNewPool(pool.tag, pool.prefab, pool.size);
            }
        }

        public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return SpawnFromPool(prefab, position, rotation, 10);
        }

        public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation, int initialSize)
        {
            if (prefab == null) return null;

            if (poolDictionary == null)
            {
                Initialize();
            }

            string tag = prefab.name;

            if (!poolDictionary.ContainsKey(tag))
            {
                CreateNewPool(tag, prefab, Mathf.Max(1, initialSize));
            }

            return SpawnFromPool(tag, position, rotation);
        }

        private Transform GetCategoryRoot(string prefabName)
        {
            string category = "Etc";
            
            if (prefabName.StartsWith("Player_")) category = "Player";
            else if (prefabName.StartsWith("Enemy_")) category = "Enemy";
            else if (prefabName.StartsWith("Effect_") || prefabName.EndsWith("VFX")) category = "VFX";
            else if (prefabName.StartsWith("UI_")) category = "UI";
            else if (prefabName.StartsWith("Item_")) category = "Item";

            if (!categoryRoots.TryGetValue(category, out Transform categoryRoot))
            {
                GameObject catObj = new GameObject($"[Category] {category}");
                catObj.transform.SetParent(this.transform);
                categoryRoot = catObj.transform;
                categoryRoots.Add(category, categoryRoot);
            }

            return categoryRoot;
        }

        private void CreateNewPool(string tag, GameObject prefab, int size)
        {
            if (poolDictionary.ContainsKey(tag)) return;

            Transform categoryRoot = GetCategoryRoot(prefab.name);

            GameObject rootObj = new GameObject($"[Pool] {tag}");
            rootObj.transform.SetParent(categoryRoot);
            poolRoots.Add(tag, rootObj.transform);

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < size; i++)
            {
                GameObject obj = container != null ? container.Instantiate(prefab) : Instantiate(prefab);
                obj.transform.SetParent(rootObj.transform);
                obj.SetActive(false);
                instancePoolTags[obj] = tag;
                objectPool.Enqueue(obj);
            }

            // 설정 목록은 풀 생성 시 한 번만 찾고, 생성·확장 시에는 태그로 바로 조회한다.
            Pool configuration = pools.Find(p => p.tag == tag);
            if (configuration == null)
            {
                configuration = new Pool { tag = tag, prefab = prefab, size = size };
                pools.Add(configuration);
            }
            poolConfigurations.Add(tag, configuration);
            
            poolDictionary.Add(tag, objectPool);
        }

        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (poolDictionary == null)
            {
                Initialize();
            }

            if (!poolDictionary.TryGetValue(tag, out Queue<GameObject> queue))
            {
                Debug.LogWarning($"Pool with tag '{tag}' doesn't exist.");
                return null;
            }

            poolConfigurations.TryGetValue(tag, out Pool pool);
            Vector3 originalScale = Vector3.one;
            if (pool != null && pool.prefab != null)
            {
                originalScale = pool.prefab.transform.localScale;
            }

            if (queue.Count == 0)
            {
                if (pool != null && pool.prefab != null)
                {
                    GameObject newObj = container != null ? container.Instantiate(pool.prefab) : Instantiate(pool.prefab);
                    instancePoolTags[newObj] = tag;
                    
                    if (poolRoots.TryGetValue(tag, out Transform root))
                    {
                        newObj.transform.SetParent(root);
                    }
                    
                    newObj.transform.position = position;
                    newObj.transform.rotation = rotation;
                    newObj.transform.localScale = originalScale;
                    newObj.SetActive(true);
                    return newObj;
                }
                return null;
            }

            GameObject obj = queue.Dequeue();
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = originalScale;
            obj.SetActive(true);
            return obj;
        }

        public bool TryGetPoolTag(GameObject instance, out string tag)
        {
            tag = null;
            return instance != null && instancePoolTags != null && instancePoolTags.TryGetValue(instance, out tag);
        }

        public void ReturnToPool(string tag, GameObject objectToReturn)
        {
            if (objectToReturn == null) return;

            if (poolDictionary == null)
            {
                Initialize();
            }

            if (instancePoolTags != null && instancePoolTags.TryGetValue(objectToReturn, out string registeredTag))
            {
                tag = registeredTag;
            }

            if (!poolDictionary.ContainsKey(tag))
            {
                Destroy(objectToReturn);
                return;
            }

            objectToReturn.SetActive(false);

            if (poolRoots.TryGetValue(tag, out Transform root))
            {
                objectToReturn.transform.SetParent(root);
            }
            else
            {
                objectToReturn.transform.SetParent(null);
            }

            poolDictionary[tag].Enqueue(objectToReturn);
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // ObjectPoolManager는 저장할 데이터가 없음 (런타임 풀 관리)
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // ObjectPoolManager는 로드할 데이터가 없음 (런타임 풀 관리)
        }
    }
}
