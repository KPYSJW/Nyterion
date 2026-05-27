using UnityEditor;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Shop;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Weapons;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using Nytherion.Data.ScriptableObjects.Progression;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Nytherion.Editor
{
    public class WeaponManagerWindow : EditorWindow
    {
        private const string DEFAULT_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Weapons";
        private const string GACHA_POOL_BASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Weapon";
        private const string WEAPON_PREFAB_PATH = "Assets/Prefabs/Weapons/Generated";
        private const string DEFAULT_SHOP_PATH = "Assets/Nytherion/Data/ScriptableObjects/Shop/Village Shop.asset";
        private const string MILESTONE_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Progression";

        private enum WindowTab { Create, Edit }
        private WindowTab currentTab = WindowTab.Create;

        private string weaponName_KR = "새 무기";
        private string weaponName_EN = "New Weapon";
        private string description_KR = "";
        private string description_EN = "";
        private WeaponType weaponType = WeaponType.Ranged;
        private Rarity rarity = Rarity.Common;
        private int defaultGachaWeight = 100;
        private float damage = 10f;
        private float range = 5f;
        private float cooldown = 0.5f;
        private int baseValue = 100;
        private Sprite weaponSprite;
        private Vector3 firePointOffset;
        private GameObject projectilePrefab;
        private float projectileSpeed = 10f;
        private ExtraProjectileMode extraMode = ExtraProjectileMode.Spread;
        private float maxChargeTime = 1.0f;
        private bool addToShop = false;
        private bool generatePrefabVariant = true;
        private bool createMilestone = false;
        private string milestoneTitle = "무기 해금: ";
        private ProgressionType milestoneType = ProgressionType.KillEnemy;
        private int milestoneTarget = 100;
        private List<EquipmentTrait> selectedTraits = new List<EquipmentTrait>();

        private static WeaponBase rangedTemplate;
        private static WeaponBase meteorTemplate;
        private static WeaponBase chargeableTemplate;
        private static WeaponBase chainLightningTemplate;

        private const string RANGED_TEMPLATE_PATH = "Assets/Prefabs/Weapons/RangedWeapon_Template.prefab";
        private const string METEOR_TEMPLATE_PATH = "Assets/Prefabs/Weapons/MeteorWeapon_Template.prefab";
        private const string CHARGEABLE_TEMPLATE_PATH = "Assets/Prefabs/Weapons/ChargeableWeapon_Template.prefab";
        private const string CHAIN_LIGHTNING_TEMPLATE_PATH = "Assets/Prefabs/Weapons/ChainLightningWeapon_Template.prefab";

        private List<WeaponData> allWeapons = new List<WeaponData>();
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private WindowTab lastTab = WindowTab.Create;
        private WeaponData selectedWeapon;

        [MenuItem("Nytherion/Weapon Manager")]
        public static void ShowWindow()
        {
            GetWindow<WeaponManagerWindow>("Weapon Manager");
        }

        private void OnEnable()
        {
            LoadDefaultTemplates();
            RefreshWeaponList();
        }

        private void LoadDefaultTemplates()
        {
            if (rangedTemplate == null) rangedTemplate = AssetDatabase.LoadAssetAtPath<WeaponBase>(RANGED_TEMPLATE_PATH);
            if (meteorTemplate == null) meteorTemplate = AssetDatabase.LoadAssetAtPath<WeaponBase>(METEOR_TEMPLATE_PATH);
            if (chargeableTemplate == null) chargeableTemplate = AssetDatabase.LoadAssetAtPath<WeaponBase>(CHARGEABLE_TEMPLATE_PATH);
            if (chainLightningTemplate == null) chainLightningTemplate = AssetDatabase.LoadAssetAtPath<WeaponBase>(CHAIN_LIGHTNING_TEMPLATE_PATH);
        }

        private void RefreshWeaponList()
        {
            AssetDatabase.Refresh(); // Ensure disk changes are picked up
            allWeapons = AssetDatabase.FindAssets("t:WeaponData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(w => w != null)
                .OrderBy(w => w.itemName_KR)
                .ToList();
        }

        private void OnGUI()
        {
            currentTab = (WindowTab)GUILayout.Toolbar((int)currentTab, new string[] { "Create New", "Edit All" });
            
            if (currentTab != lastTab)
            {
                if (currentTab == WindowTab.Edit) RefreshWeaponList();
                lastTab = currentTab;
            }

            EditorGUILayout.Space();

            if (currentTab == WindowTab.Create) DrawCreateTab();
            else DrawEditTab();
        }

        private void DrawCreateTab()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Weapon Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawTemplateSettings();
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Basic Settings", EditorStyles.miniBoldLabel);
            weaponName_KR = EditorGUILayout.TextField("Name (KR)", weaponName_KR);
            weaponName_EN = EditorGUILayout.TextField("Name (EN)", weaponName_EN);
            
            EditorGUILayout.Space(5);
            GUI.color = new Color(0.8f, 1f, 0.8f); // Light Green for KR
            EditorGUILayout.LabelField("▼ Description (KR)", EditorStyles.boldLabel);
            GUI.color = Color.white;
            description_KR = EditorGUILayout.TextArea(description_KR, GUILayout.Height(60));
            
            EditorGUILayout.Space(5);
            GUI.color = new Color(0.8f, 0.8f, 1f); // Light Blue for EN
            EditorGUILayout.LabelField("▼ Description (EN)", EditorStyles.boldLabel);
            GUI.color = Color.white;
            description_EN = EditorGUILayout.TextArea(description_EN, GUILayout.Height(60));

            EditorGUILayout.Space(10);
            rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", rarity);
            defaultGachaWeight = EditorGUILayout.IntField("Gacha Weight", defaultGachaWeight);
            baseValue = EditorGUILayout.IntField("Base Value (Price)", baseValue);
            weaponType = (WeaponType)EditorGUILayout.EnumPopup("Weapon Type", weaponType);
            damage = EditorGUILayout.FloatField("Damage", damage);
            range = EditorGUILayout.FloatField("Range", range);
            cooldown = EditorGUILayout.FloatField("Cooldown", cooldown);

            weaponSprite = (Sprite)EditorGUILayout.ObjectField("Weapon Sprite", weaponSprite, typeof(Sprite), false);
            firePointOffset = EditorGUILayout.Vector3Field("Fire Point Offset", firePointOffset);
            projectilePrefab = (GameObject)EditorGUILayout.ObjectField("Projectile Prefab", projectilePrefab, typeof(GameObject), false);
            projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", projectileSpeed);

            if (weaponType == WeaponType.Ranged)
            {
                extraMode = (ExtraProjectileMode)EditorGUILayout.EnumPopup("Extra Projectile Mode", extraMode);
                selectedLogicType = (LogicType)EditorGUILayout.EnumPopup("Logic Type", selectedLogicType);
                if (selectedLogicType == LogicType.Chargeable) maxChargeTime = EditorGUILayout.FloatField("Max Charge Time", maxChargeTime);
                else if (selectedLogicType == LogicType.Custom) manualTemplate = (WeaponBase)EditorGUILayout.ObjectField("Manual Template", manualTemplate, typeof(WeaponBase), false);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Synergy & Traits", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Select traits for this weapon to trigger synergies.", MessageType.None);
            
            var allTraits = System.Enum.GetValues(typeof(EquipmentTrait)).Cast<EquipmentTrait>().Where(t => t != EquipmentTrait.None);
            EditorGUILayout.BeginHorizontal();
            int count = 0;
            foreach (var trait in allTraits)
            {
                bool isSelected = selectedTraits.Contains(trait);
                if (EditorGUILayout.ToggleLeft(trait.ToString(), isSelected, GUILayout.Width(100)))
                {
                    if (!isSelected) selectedTraits.Add(trait);
                }
                else
                {
                    if (isSelected) selectedTraits.Remove(trait);
                }
                count++;
                if (count % 3 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Automation Options", EditorStyles.miniBoldLabel);
            generatePrefabVariant = EditorGUILayout.Toggle("Generate Prefab Variant", generatePrefabVariant);
            addToShop = EditorGUILayout.Toggle("Add to Village Shop", addToShop);
            
            EditorGUILayout.Space(5);
            createMilestone = EditorGUILayout.Toggle("Create Unlock Milestone", createMilestone);
            if (createMilestone)
            {
                EditorGUI.indentLevel++;
                milestoneTitle = EditorGUILayout.TextField("Milestone Title", milestoneTitle);
                milestoneType = (ProgressionType)EditorGUILayout.EnumPopup("Unlock Action", milestoneType);
                milestoneTarget = EditorGUILayout.IntField("Target Value", milestoneTarget);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Create Weapon", GUILayout.Height(40))) CreateWeaponData();

            EditorGUILayout.EndScrollView();
        }

        private bool ValidateInput(out string message)
        {
            if (string.IsNullOrEmpty(weaponName_EN)) { message = "English Name is required (used for filename)."; return false; }
            if (weaponSprite == null) { message = "Weapon Sprite is required."; return false; }
            if (projectilePrefab == null) { message = "Projectile Prefab is required."; return false; }
            if (GetSelectedTemplate() == null) { message = "Missing Template Prefab for selected logic."; return false; }
            
            message = "";
            return true;
        }

        private void DrawEditTab()
        {
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshWeaponList();
            
            GUI.color = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Export CSV", GUILayout.Width(100))) ExportCSV();
            GUI.color = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Import CSV", GUILayout.Width(100))) ImportCSV();
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            float listHeight = selectedWeapon == null ? position.height - 100 : position.height * 0.4f;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight));
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Name (Effective)", GUILayout.Width(150));
            GUILayout.Label("Rarity", GUILayout.Width(80));
            GUILayout.Label("Dmg", GUILayout.Width(40));
            GUILayout.Label("DPS", GUILayout.Width(40));
            GUILayout.Label("Prefab", GUILayout.Width(50));
            GUILayout.Label("Actions", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            foreach (var weapon in allWeapons)
            {
                string displayName = weapon.itemName;
                if (string.IsNullOrEmpty(displayName)) displayName = "(No Name)";

                if (!string.IsNullOrEmpty(searchFilter) && !displayName.Contains(searchFilter) && !weapon.itemName_EN.ToLower().Contains(searchFilter.ToLower()))
                    continue;

                bool isSelected = selectedWeapon == weapon;
                GUI.backgroundColor = isSelected ? new Color(0.7f, 0.7f, 1f) : Color.white;
                
                EditorGUILayout.BeginHorizontal("box");
                
                GUILayout.Label(displayName, GUILayout.Width(150));
                GUILayout.Label(weapon.rarity.ToString(), GUILayout.Width(80));
                GUILayout.Label(weapon.damage.ToString("F1"), GUILayout.Width(40));
                
                float dps = weapon.cooldown > 0 ? weapon.damage / weapon.cooldown : 0;
                GUILayout.Label(dps.ToString("F1"), GUILayout.Width(40));

                bool hasPrefab = weapon.weaponPrefab != null;
                GUI.color = hasPrefab ? Color.green : Color.red;
                GUILayout.Label(hasPrefab ? "● OK" : "○ No", GUILayout.Width(50));
                GUI.color = Color.white;

                if (GUILayout.Button("Edit", GUILayout.Width(50))) selectedWeapon = weapon;
                if (GUILayout.Button("View", GUILayout.Width(50))) Selection.activeObject = weapon;
                
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            // 하단 상세 편집 패널
            if (selectedWeapon != null)
            {
                DrawSelectedWeaponPanel();
            }

            if (GUILayout.Button("Save All Changes", GUILayout.Height(30)))
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[WeaponManager] All changes saved.");
            }
        }

        private Vector2 detailScroll;
        private void DrawSelectedWeaponPanel()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Detailed Editing: {selectedWeapon.itemName}", EditorStyles.boldLabel);
            if (GUILayout.Button("Close", GUILayout.Width(60))) selectedWeapon = null;
            EditorGUILayout.EndHorizontal();

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MaxHeight(400));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f));
            selectedWeapon.itemName_KR = EditorGUILayout.TextField("Name (KR)", selectedWeapon.itemName_KR);
            selectedWeapon.itemName_EN = EditorGUILayout.TextField("Name (EN)", selectedWeapon.itemName_EN);
            
            EditorGUILayout.Space(5);
            GUI.color = new Color(0.8f, 1f, 0.8f);
            GUILayout.Label("Description (KR)");
            GUI.color = Color.white;
            selectedWeapon.description_KR = EditorGUILayout.TextArea(selectedWeapon.description_KR, GUILayout.Height(60));
            
            EditorGUILayout.Space(5);
            GUI.color = new Color(0.8f, 0.8f, 1f);
            GUILayout.Label("Description (EN)");
            GUI.color = Color.white;
            selectedWeapon.description_EN = EditorGUILayout.TextArea(selectedWeapon.description_EN, GUILayout.Height(60));
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            selectedWeapon.rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", selectedWeapon.rarity);
            selectedWeapon.baseValue = EditorGUILayout.IntField("Base Price", selectedWeapon.baseValue);
            selectedWeapon.damage = EditorGUILayout.FloatField("Damage", selectedWeapon.damage);
            selectedWeapon.cooldown = EditorGUILayout.FloatField("Cooldown", selectedWeapon.cooldown);
            selectedWeapon.range = EditorGUILayout.FloatField("Range", selectedWeapon.range);
            
            EditorGUILayout.Space(5);
            selectedWeapon.weaponSprite = (Sprite)EditorGUILayout.ObjectField("Weapon Sprite", selectedWeapon.weaponSprite, typeof(Sprite), false);
            selectedWeapon.weaponPrefab = (WeaponBase)EditorGUILayout.ObjectField("Weapon Prefab", selectedWeapon.weaponPrefab, typeof(WeaponBase), false);
            selectedWeapon.projectilePrefab = (GameObject)EditorGUILayout.ObjectField("Projectile", selectedWeapon.projectilePrefab, typeof(GameObject), false);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (GUI.changed) EditorUtility.SetDirty(selectedWeapon);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTemplateSettings()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Default Templates", EditorStyles.miniBoldLabel);
            rangedTemplate = (WeaponBase)EditorGUILayout.ObjectField("Ranged Template", rangedTemplate, typeof(WeaponBase), false);
            meteorTemplate = (WeaponBase)EditorGUILayout.ObjectField("Meteor Template", meteorTemplate, typeof(WeaponBase), false);
            chargeableTemplate = (WeaponBase)EditorGUILayout.ObjectField("Chargeable Template", chargeableTemplate, typeof(WeaponBase), false);
            chainLightningTemplate = (WeaponBase)EditorGUILayout.ObjectField("Chain Lightning Template", chainLightningTemplate, typeof(WeaponBase), false);
            EditorGUILayout.EndVertical();
        }

        private enum LogicType { Standard, Meteor, Chargeable, ChainLightning, Custom }
        private LogicType selectedLogicType = LogicType.Standard;
        private WeaponBase manualTemplate;

        private void CreateWeaponData()
        {
            if (string.IsNullOrEmpty(weaponName_EN)) { EditorUtility.DisplayDialog("Error", "Please enter an English weapon name.", "OK"); return; }

            WeaponData newData = CreateInstance<WeaponData>();
            newData.itemName_KR = weaponName_KR;
            newData.itemName_EN = weaponName_EN;
            newData.description_KR = description_KR;
            newData.description_EN = description_EN;
            newData.rarity = rarity;
            newData.damage = damage;
            newData.range = range;
            newData.cooldown = cooldown;
            newData.baseValue = baseValue;
            newData.weaponType = weaponType;
            newData.weaponSprite = weaponSprite;
            newData.firePointOffset = firePointOffset;
            newData.projectilePrefab = projectilePrefab;
            newData.projectileSpeed = projectileSpeed;
            newData.extraProjectileMode = extraMode;
            newData.maxChargeTime = maxChargeTime;
            
            newData.traits = new List<EquipmentTrait>(selectedTraits);

            WeaponBase template = GetSelectedTemplate();
            newData.weaponPrefab = template;

            if (!Directory.Exists(DEFAULT_DATA_PATH)) Directory.CreateDirectory(DEFAULT_DATA_PATH);
            string fullPath = $"{DEFAULT_DATA_PATH}/{weaponName_EN.Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(newData, fullPath);

            if (generatePrefabVariant && template != null)
            {
                GenerateWeaponPrefab(newData, template);
            }

            if (createMilestone)
            {
                string mID = "UNLOCK_WEAPON_" + newData.itemName_EN.Replace(" ", "_").ToUpper();
                newData.unlockMilestoneID = mID;

                MilestoneData newMilestone = CreateInstance<MilestoneData>();
                newMilestone.milestoneID = mID;
                newMilestone.title = milestoneTitle;
                newMilestone.description = $"{newData.itemName_KR} 무기를 해금하기 위한 업적입니다.";
                newMilestone.progressionType = milestoneType;
                newMilestone.targetValue = milestoneTarget;
                newMilestone.icon = newData.weaponSprite;

                RewardData reward = new RewardData
                {
                    rewardType = RewardType.Item,
                    itemData = newData,
                    amount = 1
                };
                newMilestone.rewards = new List<RewardData> { reward };

                if (!Directory.Exists(MILESTONE_DATA_PATH)) Directory.CreateDirectory(MILESTONE_DATA_PATH);
                string mPath = $"{MILESTONE_DATA_PATH}/{mID}.asset";
                AssetDatabase.CreateAsset(newMilestone, mPath);
                
                AddMilestoneToDatabase(newMilestone);
                Debug.Log($"[WeaponManager] Automatically created milestone: {mPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponManager] Created weapon: {fullPath}");

            AddToItemDatabase(newData);
            AddToGachaPool(newData);
            if (addToShop) AddToVillageShop(newData);
            
            RefreshWeaponList();
        }

        private void AddMilestoneToDatabase(MilestoneData newMilestone)
        {
            string[] guids = AssetDatabase.FindAssets("t:MilestoneDatabaseSO");
            if (guids.Length > 0)
            {
                MilestoneDatabaseSO database = AssetDatabase.LoadAssetAtPath<MilestoneDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (database != null)
                {
                    if (database.allMilestones == null) database.allMilestones = new List<MilestoneData>();
                    if (!database.allMilestones.Contains(newMilestone))
                    {
                        Undo.RecordObject(database, "Add Milestone to Database");
                        database.allMilestones.Add(newMilestone);
                        database.allMilestones = database.allMilestones.OrderBy(m => m.milestoneID).ToList();
                        EditorUtility.SetDirty(database);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        private WeaponBase GetSelectedTemplate()
        {
            switch (selectedLogicType)
            {
                case LogicType.Standard: return rangedTemplate;
                case LogicType.Meteor: return meteorTemplate;
                case LogicType.Chargeable: return chargeableTemplate;
                case LogicType.ChainLightning: return chainLightningTemplate;
                case LogicType.Custom: return manualTemplate;
                default: return null;
            }
        }

        private void GenerateWeaponPrefab(WeaponData data, WeaponBase template)
        {
            if (!Directory.Exists(WEAPON_PREFAB_PATH)) Directory.CreateDirectory(WEAPON_PREFAB_PATH);

            string prefabPath = $"{WEAPON_PREFAB_PATH}/{data.itemName_EN.Replace(" ", "_")}.prefab";
            GameObject variant = (GameObject)PrefabUtility.InstantiatePrefab(template.gameObject);

            
            // Set Sprite
            if (data.weaponSprite != null)
            {
                var sr = variant.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = data.weaponSprite;
            }

            // Set Data Link
            var wb = variant.GetComponent<WeaponBase>();
            if (wb != null) wb.weaponData = data;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(variant, prefabPath);
            DestroyImmediate(variant);
            
            data.weaponPrefab = savedPrefab.GetComponent<WeaponBase>();
            EditorUtility.SetDirty(data);
        }

        private void AddToItemDatabase(ItemData newItem)
        {
            const string DB_PATH = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DB_PATH);
            if (database != null)
            {
                if (database.allItems == null) database.allItems = new List<ItemData>();
                if (!database.allItems.Contains(newItem))
                {
                    database.allItems.Add(newItem);
                    EditorUtility.SetDirty(database);
                }
            }
        }

        private void AddToGachaPool(WeaponData weapon)
        {
            string poolPath = $"{GACHA_POOL_BASE_PATH}/{weapon.rarity}_Weapon.asset";
            GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(poolPath);
            if (pool != null)
            {
                if (pool.items == null) pool.items = new List<GachaItemRate>();
                if (!pool.items.Any(r => r.item == weapon))
                {
                    pool.items.Add(new GachaItemRate { item = weapon, weight = defaultGachaWeight });
                    EditorUtility.SetDirty(pool);
                }
            }
        }

        private void AddToVillageShop(WeaponData weapon)
        {
            ShopData shop = AssetDatabase.LoadAssetAtPath<ShopData>(DEFAULT_SHOP_PATH);
            if (shop != null)
            {
                if (shop.itemsForSale == null) shop.itemsForSale = new List<ShopItemData>();
                if (!shop.itemsForSale.Any(i => i.item == weapon))
                {
                    shop.itemsForSale.Add(new ShopItemData { item = weapon, price = weapon.baseValue, stock = 1, isUnlimited = false });
                    EditorUtility.SetDirty(shop);
                }
            }
        }

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Weapons to CSV", "", "WeaponData.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            string[] headers = new string[] { "EN_Name", "KR_Name", "Rarity", "Damage", "Range", "Cooldown", "Price" };
            List<string[]> rows = new List<string[]>();

            foreach (WeaponData w in allWeapons)
            {
                rows.Add(new string[] {
                    w.itemName_EN,
                    w.itemName_KR,
                    w.rarity.ToString(),
                    w.damage.ToString(),
                    w.range.ToString(),
                    w.cooldown.ToString(),
                    w.baseValue.ToString()
                });
            }

            DataSyncUtility.ExportToCSV(path, headers, rows);
            EditorUtility.DisplayDialog("Success", "Weapons exported successfully!", "OK");
        }

        private void ImportCSV()
        {
            string path = EditorUtility.OpenFilePanel("Import Weapons from CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            List<Dictionary<string, string>> data = DataSyncUtility.ImportFromCSV(path);
            if (data == null) return;

            int updatedCount = 0;
            foreach (Dictionary<string, string> entry in data)
            {
                string enName = entry["EN_Name"];
                WeaponData weapon = allWeapons.Find(w => w.itemName_EN == enName);

                if (weapon != null)
                {
                    Undo.RecordObject(weapon, "Update Weapon from CSV");
                    if (entry.ContainsKey("KR_Name")) weapon.itemName_KR = entry["KR_Name"];
                    if (entry.ContainsKey("Rarity"))
                    {
                        if (System.Enum.TryParse(entry["Rarity"], out Rarity r)) weapon.rarity = r;
                    }
                    if (entry.ContainsKey("Damage"))
                    {
                        if (float.TryParse(entry["Damage"], out float dmg)) weapon.damage = dmg;
                    }
                    if (entry.ContainsKey("Range"))
                    {
                        if (float.TryParse(entry["Range"], out float rng)) weapon.range = rng;
                    }
                    if (entry.ContainsKey("Cooldown"))
                    {
                        if (float.TryParse(entry["Cooldown"], out float cd)) weapon.cooldown = cd;
                    }
                    if (entry.ContainsKey("Price"))
                    {
                        if (int.TryParse(entry["Price"], out int price)) weapon.baseValue = price;
                    }
                    EditorUtility.SetDirty(weapon);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            RefreshWeaponList();
            EditorUtility.DisplayDialog("Success", $"{updatedCount} weapons updated from CSV!", "OK");
        }
    }
}
