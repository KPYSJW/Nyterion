using UnityEditor;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Relics;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Core.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nytherion.Editor
{
    public class RelicManagerWindow : EditorWindow
    {
        private const string RELIC_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Relics";
        private const string DATABASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset";
        private const string MILESTONE_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Progression";

        private enum WindowTab { Create, Edit }
        private WindowTab currentTab = WindowTab.Create;

        // Creation Fields
        private RelicData creationData;
        private SerializedObject serializedCreationData;
        private SerializedProperty creationEffectModulesProp;

        private string relicName = "NewRelic";
        private string koreanName = "새 유물";
        private string englishName = "";
        private string description_KR = "";
        private string description_EN = "";
        private Sprite relicImage;
        private Rarity rarity = Rarity.Common;
        private int level = 1;

        // Milestone Creation Fields
        private bool createMilestone = false;
        private string milestoneTitle = "유물 해금: ";
        private ProgressionType milestoneType = ProgressionType.KillEnemy;
        private int milestoneTarget = 100;

        private const int InfluenceRadius = 4;
        private const int InfluenceGridSize = InfluenceRadius * 2 + 1;
        private InfluenceType[,] influenceGrid = new InfluenceType[InfluenceGridSize, InfluenceGridSize];
        private int[,] influenceAmounts = new int[InfluenceGridSize, InfluenceGridSize];
        private bool creationUsesCustomInfluence;
        private Vector2Int selectedInfluenceCell = Vector2Int.zero;
        private InfluenceType[,] savedCreationGrid;
        private int[,] savedCreationAmounts;
        
        // Edit Mode Fields
        private List<RelicData> allRelics = new List<RelicData>();
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private RelicData selectedRelic;
        private SerializedObject serializedRelic;
        private SerializedProperty effectModulesProp;
        private WindowTab lastTab = WindowTab.Create;

        [MenuItem("Nytherion/Relic Manager")]
        public static void ShowWindow()
        {
            GetWindow<RelicManagerWindow>("Relic Manager");
        }

        private void OnEnable()
        {
            RefreshRelicList();
            creationUsesCustomInfluence = false;
            ResetInfluenceGrid(rarity);
            InitializeCreationData();
        }

        private void InitializeCreationData()
        {
            creationData = ScriptableObject.CreateInstance<RelicData>();
            serializedCreationData = new SerializedObject(creationData);
            creationEffectModulesProp = serializedCreationData.FindProperty("effectModules");
        }

        private void ResetInfluenceGrid(Rarity targetRarity)
        {
            ClearInfluenceGrid();

            foreach (InfluenceZone zone in RelicInfluencePolicy.GetDefaultZones(targetRarity))
            {
                influenceGrid[zone.offset.x + InfluenceRadius, zone.offset.y + InfluenceRadius] = zone.type;
                influenceAmounts[zone.offset.x + InfluenceRadius, zone.offset.y + InfluenceRadius] = zone.levelAmount;
            }
        }

        private void ClearInfluenceGrid()
        {
            for (int x = 0; x < InfluenceGridSize; x++)
                for (int y = 0; y < InfluenceGridSize; y++)
                {
                    influenceGrid[x, y] = InfluenceType.None;
                    influenceAmounts[x, y] = 1;
                }
            selectedInfluenceCell = Vector2Int.zero;
        }

        private void RefreshRelicList()
        {
            AssetDatabase.Refresh();
            allRelics = AssetDatabase.FindAssets("t:RelicData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(r => r != null)
                .OrderBy(r => r.koreanName)
                .ToList();
        }

        private void OnGUI()
        {
            currentTab = (WindowTab)GUILayout.Toolbar((int)currentTab, new string[] { "Create New", "Edit All" });
            
            if (currentTab != lastTab)
            {
                if (currentTab == WindowTab.Edit)
                {
                    savedCreationGrid = (InfluenceType[,])influenceGrid.Clone();
                    savedCreationAmounts = (int[,])influenceAmounts.Clone();
                    RefreshRelicList();
                }
                else if (savedCreationGrid != null)
                {
                    influenceGrid = savedCreationGrid;
                    influenceAmounts = savedCreationAmounts;
                    selectedInfluenceCell = Vector2Int.zero;
                }
                lastTab = currentTab;
            }

            RelicDatabaseSO database = GetDatabase();
            int dbCount = database != null && database.allRelics != null ? database.allRelics.Count : 0;
            int dbUniqueCount = database != null && database.allRelics != null ? database.allRelics.Where(r => r != null).Distinct().Count() : 0;

            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label($"전체 에셋 수: {allRelics.Count}개", EditorStyles.boldLabel);
            GUILayout.Label($"|   DB 등록 수: {dbCount}개 (고유 유물: {dbUniqueCount}개)", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (currentTab == WindowTab.Create) DrawCreateTab();
            else DrawEditTab();
        }

        private void DrawCreateTab()
        {
            if (serializedCreationData == null) InitializeCreationData();
            serializedCreationData.Update();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Relic Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Basic Info", EditorStyles.miniBoldLabel);
            relicName = EditorGUILayout.TextField("ID", relicName);
            koreanName = EditorGUILayout.TextField("Name (KR)", koreanName);
            englishName = EditorGUILayout.TextField("Name (EN)", englishName);
            
            GUILayout.Label("Description (KR)");
            description_KR = EditorGUILayout.TextArea(description_KR, GUILayout.Height(60));
            GUILayout.Label("Description (EN)");
            description_EN = EditorGUILayout.TextArea(description_EN, GUILayout.Height(60));

            Rarity previousRarity = rarity;
            rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", rarity);
            if (rarity != previousRarity && !creationUsesCustomInfluence)
            {
                ResetInfluenceGrid(rarity);
            }
            level = EditorGUILayout.IntField("Initial Level", level);
            relicImage = (Sprite)EditorGUILayout.ObjectField("Relic Image", relicImage, typeof(Sprite), false);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Effect Modules", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(creationEffectModulesProp, true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            
            DrawInfluenceGridEditor();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Unlock Achievement (Milestone)", EditorStyles.miniBoldLabel);
            createMilestone = EditorGUILayout.Toggle("Create Unlock Milestone?", createMilestone);
            if (createMilestone)
            {
                milestoneTitle = EditorGUILayout.TextField("Milestone Title", milestoneTitle);
                milestoneType = (ProgressionType)EditorGUILayout.EnumPopup("Milestone Type", milestoneType);
                milestoneTarget = EditorGUILayout.IntField("Target Value", milestoneTarget);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Relic Data", GUILayout.Height(40))) CreateRelicData();

            EditorGUILayout.EndScrollView();

            serializedCreationData.ApplyModifiedProperties();
        }

        private void DrawEditTab()
        {
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshRelicList();
            if (GUILayout.Button("Sync DB", GUILayout.Width(80))) SyncAllToDatabase();

            GUI.color = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Export CSV", GUILayout.Width(100))) ExportCSV();
            GUI.color = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Import CSV", GUILayout.Width(100))) ImportCSV();
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Check if any relics are missing from database
            CheckDatabaseSyncStatus();

            float listHeight = selectedRelic == null ? position.height - 120 : position.height * 0.4f;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight));
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Name (KR)", GUILayout.Width(150));
            GUILayout.Label("Rarity", GUILayout.Width(80));
            GUILayout.Label("Level", GUILayout.Width(40));
            GUILayout.Label("Zones", GUILayout.Width(40));
            GUILayout.Label("Actions", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            foreach (var relic in allRelics)
            {
                if (!string.IsNullOrEmpty(searchFilter) && !relic.koreanName.Contains(searchFilter) && !relic.relicName.ToLower().Contains(searchFilter.ToLower()))
                    continue;

                bool isSelected = selectedRelic == relic;
                GUI.backgroundColor = isSelected ? new Color(0.7f, 0.7f, 1f) : Color.white;
                
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(relic.koreanName, GUILayout.Width(150));
                GUILayout.Label(relic.rarity.ToString(), GUILayout.Width(80));
                GUILayout.Label(relic.level.ToString(), GUILayout.Width(40));
                GUILayout.Label(relic.influenceZones.Count.ToString(), GUILayout.Width(40));

                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                {
                    selectedRelic = relic;
                    serializedRelic = new SerializedObject(selectedRelic);
                    effectModulesProp = serializedRelic.FindProperty("effectModules");
                    LoadRelicToGrid(relic);
                }
                if (GUILayout.Button("View", GUILayout.Width(50))) Selection.activeObject = relic;
                
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            if (selectedRelic != null) DrawSelectedRelicPanel();

            if (GUILayout.Button("Save All Changes", GUILayout.Height(30)))
            {
                AssetDatabase.SaveAssets();
                SyncAllToDatabase(); // Ensure DB is synced on save
                Debug.Log("[RelicManager] All changes saved and database synced.");
            }
        }

        private void CheckDatabaseSyncStatus()
        {
            RelicDatabaseSO database = GetDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("Relic Database not found at expected path!", MessageType.Error);
                return;
            }

            int missingCount = allRelics.Count(r => !database.allRelics.Contains(r));
            if (missingCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = Color.yellow;
                EditorGUILayout.HelpBox($"{missingCount} relics are missing from the database.", MessageType.Warning);
                GUI.color = Color.white;
                if (GUILayout.Button("Fix Now", GUILayout.Height(38)))
                {
                    SyncAllToDatabase();
                }
                EditorGUILayout.EndHorizontal();
            }

            int duplicateCount = database.allRelics.Count - database.allRelics.Where(r => r != null).Distinct().Count();
            if (duplicateCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = new Color(1f, 0.6f, 0.6f);
                EditorGUILayout.HelpBox($"{duplicateCount} duplicate references found in the database.", MessageType.Warning);
                GUI.color = Color.white;
                if (GUILayout.Button("Remove Duplicates", GUILayout.Height(38)))
                {
                    FixDatabaseDuplicates();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void FixDatabaseDuplicates()
        {
            RelicDatabaseSO database = GetDatabase();
            if (database == null) return;

            Undo.RecordObject(database, "Fix Relic Database Duplicates");
            List<RelicData> uniqueRelics = database.allRelics.Where(r => r != null).Distinct().OrderBy(r => r.koreanName).ToList();
            database.allRelics = uniqueRelics;

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log("[RelicManager] Database duplicates removed successfully.");
        }

        private static RelicDatabaseSO GetDatabase()
        {
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(DATABASE_PATH);
            if (database == null)
            {
                // Try finding by type if path fails
                string[] guids = AssetDatabase.FindAssets("t:RelicDatabaseSO");
                if (guids.Length > 0)
                {
                    database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            return database;
        }

        private void SyncAllToDatabase()
        {
            SyncAllToDatabase(allRelics);
        }

        private static void SyncAllToDatabase(List<RelicData> relics)
        {
            RelicDatabaseSO database = GetDatabase();
            if (database == null)
            {
                Debug.LogError("[RelicManager] Failed to find RelicDatabaseSO.");
                return;
            }

            Undo.RecordObject(database, "Sync Relic Database");
            if (database.allRelics == null) database.allRelics = new List<RelicData>();

            int addedCount = 0;
            foreach (RelicData relic in relics)
            {
                if (!database.allRelics.Contains(relic))
                {
                    database.allRelics.Add(relic);
                    addedCount++;
                }
            }

            database.allRelics.RemoveAll(r => r == null);
            database.allRelics = database.allRelics.Distinct().OrderBy(r => r.koreanName).ToList();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            if (addedCount > 0)
                Debug.Log($"[RelicManager] Database synced. {addedCount} relics added.");
        }

        private void LoadRelicToGrid(RelicData relic)
        {
            if (RelicInfluencePolicy.Normalize(relic))
            {
                EditorUtility.SetDirty(relic);
            }
            ClearInfluenceGrid();
            if (relic.influenceZones == null) return;

            foreach (var zone in relic.influenceZones)
            {
                if (zone == null) continue;
                int x = zone.offset.x + InfluenceRadius;
                int y = zone.offset.y + InfluenceRadius;
                if (x >= 0 && x < InfluenceGridSize && y >= 0 && y < InfluenceGridSize)
                {
                    influenceGrid[x, y] = zone.type;
                    influenceAmounts[x, y] = zone.GetLevelAmount();
                }
            }
        }

        private bool SyncGridToRelic(RelicData relic)
        {
            List<InfluenceZone> zones = new List<InfluenceZone>();
            for (int x = 0; x < InfluenceGridSize; x++)
            {
                for (int y = 0; y < InfluenceGridSize; y++)
                {
                    InfluenceType type = influenceGrid[x, y];
                    if (x == InfluenceRadius && y == InfluenceRadius || type == InfluenceType.None) continue;
                    zones.Add(new InfluenceZone
                    {
                        offset = new Vector2Int(x - InfluenceRadius, y - InfluenceRadius),
                        type = type,
                        levelAmount = influenceAmounts[x, y]
                    });
                }
            }
            if (!RelicInfluencePolicy.TryValidateInfluenceZones(zones, out string error))
            {
                EditorUtility.DisplayDialog("영향 범위 오류", error, "확인");
                return false;
            }
            relic.influenceZones = zones;
            EditorUtility.SetDirty(relic);
            return true;
        }

        private Vector2 detailScroll;
        private void DrawSelectedRelicPanel()
        {
            if (serializedRelic == null || serializedRelic.targetObject != selectedRelic)
            {
                serializedRelic = new SerializedObject(selectedRelic);
                effectModulesProp = serializedRelic.FindProperty("effectModules");
            }

            serializedRelic.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Detailed Editing: {selectedRelic.koreanName}", EditorStyles.boldLabel);
            if (GUILayout.Button("Close", GUILayout.Width(60))) 
            {
                selectedRelic = null;
                serializedRelic = null;
                return;
            }
            EditorGUILayout.EndHorizontal();

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MaxHeight(600));

            EditorGUILayout.BeginHorizontal();
            // Left: Info
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
            selectedRelic.koreanName = EditorGUILayout.TextField("Name (KR)", selectedRelic.koreanName);
            selectedRelic.relicName = EditorGUILayout.TextField("ID", selectedRelic.relicName);
            selectedRelic.englishName = EditorGUILayout.TextField("Name (EN)", selectedRelic.englishName);
            
            GUILayout.Label("Description (KR)");
            selectedRelic.description_KR = EditorGUILayout.TextArea(selectedRelic.description_KR, GUILayout.Height(60));
            GUILayout.Label("Description (EN)");
            selectedRelic.description_EN = EditorGUILayout.TextArea(selectedRelic.description_EN, GUILayout.Height(60));
            
            EditorGUILayout.Space(5);
            Rarity previousRarity = selectedRelic.rarity;
            selectedRelic.rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", selectedRelic.rarity);
            if (selectedRelic.rarity != previousRarity)
            {
                if (!RelicInfluencePolicy.IsSpecialRelic(selectedRelic)) RelicInfluencePolicy.Normalize(selectedRelic);
                LoadRelicToGrid(selectedRelic);
            }
            selectedRelic.level = EditorGUILayout.IntField("Level", selectedRelic.level);
            selectedRelic.Image = (Sprite)EditorGUILayout.ObjectField("Image", selectedRelic.Image, typeof(Sprite), false);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            DrawInfluenceGridEditor(selectedRelic);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            selectedRelic.synergySeriesId = EditorGUILayout.TextField("Synergy Series ID", selectedRelic.synergySeriesId);
            selectedRelic.synergySetBonusData = (RelicSetBonusData)EditorGUILayout.ObjectField(
                "Set Bonus Data",
                selectedRelic.synergySetBonusData,
                typeof(RelicSetBonusData),
                false);

            EditorGUILayout.Space(10);
            selectedRelic.unlockMilestoneID = EditorGUILayout.TextField("Unlock Milestone ID", selectedRelic.unlockMilestoneID);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Effect Modules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(effectModulesProp, true);

            if (serializedRelic.ApplyModifiedProperties() || GUI.changed) 
            {
                EditorUtility.SetDirty(selectedRelic);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawInfluenceGridEditor(RelicData targetRelic = null)
        {
            Rarity targetRarity = targetRelic != null ? targetRelic.rarity : rarity;
            bool custom = targetRelic != null
                ? RelicInfluencePolicy.IsSpecialRelic(targetRelic)
                : creationUsesCustomInfluence;

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("유물 영향 범위", EditorStyles.miniBoldLabel);
            bool newCustom = EditorGUILayout.Toggle("유물별 개별 설정", custom);
            if (newCustom != custom)
            {
                if (targetRelic != null)
                {
                    Undo.RecordObject(targetRelic, "유물 영향 범위 변경");
                    targetRelic.preserveCustomInfluenceZones = newCustom;
                    if (!newCustom)
                    {
                        ResetInfluenceGrid(targetRarity);
                        SyncGridToRelic(targetRelic);
                    }
                    EditorUtility.SetDirty(targetRelic);
                }
                else
                {
                    creationUsesCustomInfluence = newCustom;
                    if (!newCustom) ResetInfluenceGrid(targetRarity);
                }
                custom = newCustom;
            }

            if (targetRelic != null &&
                !RelicInfluencePolicy.TryValidateInfluenceZones(targetRelic.influenceZones, out string validationError))
            {
                EditorGUILayout.HelpBox(validationError + " 기존 설정을 유지하고 있습니다. 범위를 초기화하거나 Inspector에서 수정해 주세요.", MessageType.Error);
                if (GUILayout.Button("등급 기본 범위로 초기화"))
                {
                    Undo.RecordObject(targetRelic, "유물 영향 범위 초기화");
                    targetRelic.preserveCustomInfluenceZones = true;
                    ResetInfluenceGrid(targetRarity);
                    SyncGridToRelic(targetRelic);
                }
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(custom
                ? "칸을 눌러 레벨 증가, 감소, 침묵, 비우기 순으로 변경합니다. 유물을 포함한 전체 범위는 최대 5×5입니다."
                : "등급별 기본 범위를 사용합니다. 개별 설정을 켜면 범위를 직접 편집할 수 있습니다.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            for (int y = InfluenceGridSize - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < InfluenceGridSize; x++)
                {
                    bool center = x == InfluenceRadius && y == InfluenceRadius;
                    InfluenceType type = influenceGrid[x, y];
                    if (center) GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                    else SetGridColor(type);
                    bool allowed = custom && !center &&
                        (type != InfluenceType.None || CanAddInfluenceCell(x, y));
                    EditorGUI.BeginDisabledGroup(!allowed);
                    string label = center ? "유물" : GetInfluenceLabel(type, influenceAmounts[x, y]);
                    if (GUILayout.Button(label, GUILayout.Width(43), GUILayout.Height(32)))
                    {
                        if (targetRelic != null)
                        {
                            Undo.RecordObject(targetRelic, "유물 영향 칸 변경");
                            targetRelic.preserveCustomInfluenceZones = true;
                        }
                        selectedInfluenceCell = new Vector2Int(x, y);
                        CycleInfluenceType(x, y, true);
                        if (targetRelic != null) SyncGridToRelic(targetRelic);
                    }
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            int selectedX = selectedInfluenceCell.x;
            int selectedY = selectedInfluenceCell.y;
            if (custom && (influenceGrid[selectedX, selectedY] == InfluenceType.LevelUp ||
                           influenceGrid[selectedX, selectedY] == InfluenceType.LevelDown))
            {
                int amount = EditorGUILayout.IntSlider("선택한 칸의 레벨 변동량", influenceAmounts[selectedX, selectedY], 1, 3);
                if (amount != influenceAmounts[selectedX, selectedY])
                {
                    if (targetRelic != null)
                    {
                        Undo.RecordObject(targetRelic, "유물 영향 수치 변경");
                        targetRelic.preserveCustomInfluenceZones = true;
                    }
                    influenceAmounts[selectedX, selectedY] = amount;
                    if (targetRelic != null) SyncGridToRelic(targetRelic);
                }
            }

            if (custom && GUILayout.Button("등급 기본 범위로 초기화"))
            {
                if (targetRelic != null)
                {
                    Undo.RecordObject(targetRelic, "유물 영향 범위 초기화");
                    targetRelic.preserveCustomInfluenceZones = true;
                }
                ResetInfluenceGrid(targetRarity);
                if (targetRelic != null) SyncGridToRelic(targetRelic);
            }
            EditorGUILayout.EndVertical();
        }

        private bool CanAddInfluenceCell(int candidateX, int candidateY)
        {
            int minX = Mathf.Min(InfluenceRadius, candidateX);
            int maxX = Mathf.Max(InfluenceRadius, candidateX);
            int minY = Mathf.Min(InfluenceRadius, candidateY);
            int maxY = Mathf.Max(InfluenceRadius, candidateY);
            for (int x = 0; x < InfluenceGridSize; x++)
                for (int y = 0; y < InfluenceGridSize; y++)
                {
                    if (influenceGrid[x, y] == InfluenceType.None) continue;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            return maxX - minX <= 4 && maxY - minY <= 4;
        }

        private void SetGridColor(InfluenceType type)
        {
            switch (type)
            {
                case InfluenceType.LevelUp: GUI.backgroundColor = new Color(0.4f, 0.6f, 1f); break; // Blue
                case InfluenceType.LevelDown: GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); break; // Red
                case InfluenceType.Silence: GUI.backgroundColor = new Color(0.8f, 0.4f, 1f); break; // Purple
                default: GUI.backgroundColor = Color.white; break;
            }
        }

        private void CycleInfluenceType(int x, int y, bool allowEmpty)
        {
            switch (influenceGrid[x, y])
            {
                case InfluenceType.LevelUp:
                    influenceGrid[x, y] = InfluenceType.LevelDown;
                    break;
                case InfluenceType.LevelDown:
                    influenceGrid[x, y] = InfluenceType.Silence;
                    break;
                case InfluenceType.Silence:
                    influenceGrid[x, y] = allowEmpty ? InfluenceType.None : InfluenceType.LevelUp;
                    break;
                default:
                    influenceGrid[x, y] = InfluenceType.LevelUp;
                    break;
            }
        }

        private static string GetInfluenceLabel(InfluenceType type, int levelAmount)
        {
            switch (type)
            {
                case InfluenceType.LevelUp:
                    return $"Up +{Mathf.Clamp(levelAmount, 1, 3)}";
                case InfluenceType.LevelDown:
                    return $"Down -{Mathf.Clamp(levelAmount, 1, 3)}";
                case InfluenceType.Silence:
                    return "Silence";
                default:
                    return "";
            }
        }

        private void CreateRelicData()
        {
            if (string.IsNullOrEmpty(relicName)) { EditorUtility.DisplayDialog("Error", "Please enter an English name.", "OK"); return; }

            RelicData newData = ScriptableObject.CreateInstance<RelicData>();
            newData.relicName = relicName;
            newData.koreanName = koreanName;
            newData.englishName = englishName;
            newData.description_KR = description_KR;
            newData.description_EN = description_EN;
            newData.Image = relicImage;
            newData.rarity = rarity;
            newData.level = level;

            if (creationData.effectModules != null)
            {
                newData.effectModules = new List<Nytherion.Gameplay.Relics.Modules.RelicEffectModule>(creationData.effectModules);
            }

            newData.preserveCustomInfluenceZones = creationUsesCustomInfluence;
            if (!SyncGridToRelic(newData))
            {
                DestroyImmediate(newData);
                return;
            }

            // Automated Milestone Creation
            if (createMilestone)
            {
                string mID = "UNLOCK_RELIC_" + relicName.ToUpper();
                newData.unlockMilestoneID = mID;

                MilestoneData newMilestone = ScriptableObject.CreateInstance<MilestoneData>();
                newMilestone.milestoneID = mID;
                newMilestone.title = milestoneTitle;
                newMilestone.description = $"{koreanName} 유물을 해금하기 위한 업적입니다.";
                newMilestone.progressionType = milestoneType;
                newMilestone.targetValue = milestoneTarget;
                newMilestone.icon = relicImage;

                RewardData reward = new RewardData
                {
                    rewardType = RewardType.RelicUnlock,
                    relicData = newData,
                    amount = 1
                };
                newMilestone.rewards = new List<RewardData> { reward };

                if (!Directory.Exists(MILESTONE_DATA_PATH)) Directory.CreateDirectory(MILESTONE_DATA_PATH);
                string mPath = $"{MILESTONE_DATA_PATH}/{mID}.asset";
                AssetDatabase.CreateAsset(newMilestone, mPath);
                
                AddMilestoneToDatabase(newMilestone);
                Debug.Log($"[RelicManager] Automatically created milestone: {mPath}");
            }

            if (!Directory.Exists(RELIC_DATA_PATH)) Directory.CreateDirectory(RELIC_DATA_PATH);
            string fullPath = $"{RELIC_DATA_PATH}/{relicName.Replace(" ", "_")}.asset";
            AssetDatabase.CreateAsset(newData, fullPath);
            AssetDatabase.SaveAssets();

            AddToDatabase(newData);
            AddToGachaPool(newData);
            RefreshRelicList();
            Selection.activeObject = newData;
            Debug.Log($"[RelicManager] Created relic: {fullPath}");

            relicName = "NewRelic";
            koreanName = "새 유물";
            englishName = "";
            description_KR = "";
            description_EN = "";
            relicImage = null;
            createMilestone = false;
            milestoneTitle = "유물 해금: ";
            creationUsesCustomInfluence = false;
            ResetInfluenceGrid(rarity);
            InitializeCreationData(); 
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

        private static void AddToDatabase(RelicData newRelic)
        {
            RelicDatabaseSO database = GetDatabase();
            if (database != null)
            {
                if (database.allRelics == null) database.allRelics = new List<RelicData>();
                if (!database.allRelics.Contains(newRelic))
                {
                    Undo.RecordObject(database, "Add Relic to Database");
                    database.allRelics.Add(newRelic);
                    database.allRelics = database.allRelics.OrderBy(r => r.koreanName).ToList();
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[RelicManager] Automatically registered new relic to database: {newRelic.koreanName}");
                }
            }
        }

        private static void AddToGachaPool(RelicData relic)
        {
            string poolPath = $"Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic/{relic.rarity}_Relic.asset";
            Nytherion.Data.ScriptableObjects.Gacha.GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<Nytherion.Data.ScriptableObjects.Gacha.GachaPoolSO>(poolPath);
            if (pool != null)
            {
                if (pool.items == null) pool.items = new List<Nytherion.Data.ScriptableObjects.Gacha.GachaItemRate>();
                if (!pool.items.Any(i => i.item == relic))
                {
                    Undo.RecordObject(pool, "Add Relic to Gacha Pool");
                    pool.items.Add(new Nytherion.Data.ScriptableObjects.Gacha.GachaItemRate { item = relic, weight = 100 });
                    EditorUtility.SetDirty(pool);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[RelicManager] Automatically registered new relic to gacha pool: {relic.koreanName}");
                }
            }
        }

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Relics to CSV", "", "RelicData.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            string[] headers = new string[] { 
                "EN_Name", "KR_Name", "Rarity", "Level", "Description_KR",
                "Synergy_Series_ID", "Unlock_Milestone_ID",
                "Influence_Zones",
                "Stat1_Type", "Stat1_Value", "Stat1_ValuePerLevel", "Stat1_IsPercentage",
                "Stat2_Type", "Stat2_Value", "Stat2_ValuePerLevel", "Stat2_IsPercentage",
                "Custom_Influence_Zones"
            };
            List<string[]> rows = new List<string[]>();

            foreach (RelicData r in allRelics)
            {
                string synergySeriesId = r.synergySeriesId ?? "";
                string unlockMilestoneID = r.unlockMilestoneID ?? "";

                string influenceZonesStr = "";
                if (r.influenceZones != null && r.influenceZones.Count > 0)
                {
                    List<string> zoneStrings = new List<string>();
                    foreach (InfluenceZone zone in r.influenceZones)
                    {
                        zoneStrings.Add($"{zone.offset.x},{zone.offset.y}:{zone.type}:{zone.GetLevelAmount()}");
                    }
                    influenceZonesStr = string.Join(";", zoneStrings);
                }

                string stat1Type = "";
                string stat1Value = "";
                string stat1ValuePerLevel = "";
                string stat1IsPercentage = "";

                string stat2Type = "";
                string stat2Value = "";
                string stat2ValuePerLevel = "";
                string stat2IsPercentage = "";

                if (r is SimpleStatRelicData simpleRelic && simpleRelic.simpleStatModifiers != null)
                {
                    if (simpleRelic.simpleStatModifiers.Count > 0 && simpleRelic.simpleStatModifiers[0] != null)
                    {
                        StatModifier m1 = simpleRelic.simpleStatModifiers[0];
                        stat1Type = m1.stat.ToString();
                        stat1Value = m1.value.ToString();
                        stat1ValuePerLevel = m1.valuePerLevel.ToString();
                        stat1IsPercentage = m1.isPercentage.ToString().ToUpper();
                    }
                    if (simpleRelic.simpleStatModifiers.Count > 1 && simpleRelic.simpleStatModifiers[1] != null)
                    {
                        StatModifier m2 = simpleRelic.simpleStatModifiers[1];
                        stat2Type = m2.stat.ToString();
                        stat2Value = m2.value.ToString();
                        stat2ValuePerLevel = m2.valuePerLevel.ToString();
                        stat2IsPercentage = m2.isPercentage.ToString().ToUpper();
                    }
                }

                rows.Add(new string[] {
                    r.relicName,
                    r.koreanName,
                    r.rarity.ToString(),
                    r.level.ToString(),
                    r.description_KR,
                    synergySeriesId,
                    unlockMilestoneID,
                    influenceZonesStr,
                    stat1Type,
                    stat1Value,
                    stat1ValuePerLevel,
                    stat1IsPercentage,
                    stat2Type,
                    stat2Value,
                    stat2ValuePerLevel,
                    stat2IsPercentage,
                    r.preserveCustomInfluenceZones.ToString()
                });
            }

            DataSyncUtility.ExportToCSV(path, headers, rows);
            EditorUtility.DisplayDialog("Success", "Relics exported successfully!", "OK");
        }

        private static bool ImportInfluenceZones(Dictionary<string, string> entry, RelicData relic)
        {
            if (!entry.TryGetValue("Influence_Zones", out string zonesText)) return true;
            List<InfluenceZone> zones = new List<InfluenceZone>();
            if (!string.IsNullOrEmpty(zonesText))
            {
                foreach (string item in zonesText.Split(';'))
                {
                    if (string.IsNullOrEmpty(item)) continue;
                    string[] parts = item.Split(':');
                    string[] coords = parts[0].Split(',');
                    if ((parts.Length != 2 && parts.Length != 3) || coords.Length != 2 ||
                        !int.TryParse(coords[0], out int x) || !int.TryParse(coords[1], out int y) ||
                        !System.Enum.TryParse(parts[1], out InfluenceType type) || !System.Enum.IsDefined(typeof(InfluenceType), type))
                    {
                        Debug.LogWarning($"[RelicManager] {relic.relicName}: 잘못된 영향 칸 '{item}' 때문에 기존 범위를 유지합니다.");
                        return false;
                    }
                    int amount = 1;
                    if (parts.Length == 3 && (!int.TryParse(parts[2], out amount) || amount < 1 || amount > 3))
                    {
                        Debug.LogWarning($"[RelicManager] {relic.relicName}: 잘못된 영향 수치 '{item}' 때문에 기존 범위를 유지합니다.");
                        return false;
                    }
                    zones.Add(new InfluenceZone { offset = new Vector2Int(x, y), type = type, levelAmount = amount });
                }
            }

            if (!RelicInfluencePolicy.TryValidateInfluenceZones(zones, out string error))
            {
                Debug.LogWarning($"[RelicManager] {relic.relicName}: 영향 범위를 가져오지 않았습니다. {error}");
                return false;
            }
            bool custom = relic.preserveCustomInfluenceZones;
            if (entry.TryGetValue("Custom_Influence_Zones", out string customText) &&
                !string.IsNullOrEmpty(customText) && !bool.TryParse(customText, out custom))
            {
                Debug.LogWarning($"[RelicManager] {relic.relicName}: 잘못된 개별 설정 값 때문에 기존 범위를 유지합니다.");
                return false;
            }
            relic.influenceZones = zones;
            relic.preserveCustomInfluenceZones = custom;
            return true;
        }

        private void ImportCSV()
        {
            string path = EditorUtility.OpenFilePanel("Import Relics from CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            List<Dictionary<string, string>> data = DataSyncUtility.ImportFromCSV(path);
            if (data == null) return;

            int updatedCount = 0;
            int createdCount = 0;

            foreach (Dictionary<string, string> entry in data)
            {
                if (!entry.ContainsKey("EN_Name") || string.IsNullOrEmpty(entry["EN_Name"])) continue;

                string enName = entry["EN_Name"];
                RelicData relic = allRelics.Find(r => r.relicName == enName);
                bool isNew = (relic == null);

                if (isNew)
                {
                    SimpleStatRelicData newRelic = ScriptableObject.CreateInstance<SimpleStatRelicData>();
                    newRelic.relicName = enName;
                    relic = newRelic;
                }

                Undo.RecordObject(relic, "Update Relic from CSV");

                if (entry.ContainsKey("KR_Name")) relic.koreanName = entry["KR_Name"];
                if (entry.ContainsKey("Rarity"))
                {
                    if (System.Enum.TryParse(entry["Rarity"], out Rarity rarity))
                        relic.rarity = rarity;
                }
                if (entry.ContainsKey("Level"))
                {
                    if (int.TryParse(entry["Level"], out int level))
                        relic.level = level;
                }
                if (entry.ContainsKey("Description_KR")) relic.description_KR = entry["Description_KR"];
                if (entry.ContainsKey("Synergy_Series_ID")) relic.synergySeriesId = entry["Synergy_Series_ID"];
                if (entry.ContainsKey("Unlock_Milestone_ID")) relic.unlockMilestoneID = entry["Unlock_Milestone_ID"];

                if (ImportInfluenceZones(entry, relic))
                    RelicInfluencePolicy.Normalize(relic);

                if (relic is SimpleStatRelicData simpleRelic)
                {
                    if (simpleRelic.simpleStatModifiers == null)
                    {
                        simpleRelic.simpleStatModifiers = new List<StatModifier>();
                    }
                    simpleRelic.simpleStatModifiers.Clear();

                    if (entry.ContainsKey("Stat1_Type") && !string.IsNullOrEmpty(entry["Stat1_Type"]))
                    {
                        if (System.Enum.TryParse(entry["Stat1_Type"], out StatType stat1Type) && !stat1Type.IsRetired())
                        {
                            float val = 0f;
                            float valPerLvl = 0f;
                            bool isPct = false;

                            if (entry.ContainsKey("Stat1_Value")) float.TryParse(entry["Stat1_Value"], out val);
                            if (entry.ContainsKey("Stat1_ValuePerLevel")) float.TryParse(entry["Stat1_ValuePerLevel"], out valPerLvl);
                            if (entry.ContainsKey("Stat1_IsPercentage"))
                            {
                                string isPctStr = entry["Stat1_IsPercentage"].ToLower();
                                isPct = (isPctStr == "true" || isPctStr == "1");
                            }

                            StatModifier modifier1 = new StatModifier
                            {
                                stat = stat1Type,
                                value = val,
                                valuePerLevel = valPerLvl,
                                isPercentage = isPct
                            };
                            simpleRelic.simpleStatModifiers.Add(modifier1);
                        }
                    }

                    if (entry.ContainsKey("Stat2_Type") && !string.IsNullOrEmpty(entry["Stat2_Type"]))
                    {
                        if (System.Enum.TryParse(entry["Stat2_Type"], out StatType stat2Type) && !stat2Type.IsRetired())
                        {
                            float val = 0f;
                            float valPerLvl = 0f;
                            bool isPct = false;

                            if (entry.ContainsKey("Stat2_Value")) float.TryParse(entry["Stat2_Value"], out val);
                            if (entry.ContainsKey("Stat2_ValuePerLevel")) float.TryParse(entry["Stat2_ValuePerLevel"], out valPerLvl);
                            if (entry.ContainsKey("Stat2_IsPercentage"))
                            {
                                string isPctStr = entry["Stat2_IsPercentage"].ToLower();
                                isPct = (isPctStr == "true" || isPctStr == "1");
                            }

                            StatModifier modifier2 = new StatModifier
                            {
                                stat = stat2Type,
                                value = val,
                                valuePerLevel = valPerLvl,
                                isPercentage = isPct
                            };
                            simpleRelic.simpleStatModifiers.Add(modifier2);
                        }
                    }

                    simpleRelic.InitializeSimpleStats(true);
                }

                EditorUtility.SetDirty(relic);

                if (isNew)
                {
                    string directoryPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats";
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    string assetPath = $"{directoryPath}/{enName.Replace(" ", "_")}.asset";
                    AssetDatabase.CreateAsset(relic, assetPath);

                    AddToDatabase(relic);
                    AddToGachaPool(relic);
                    createdCount++;
                }
                else
                {
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            RefreshRelicList();
            SyncAllToDatabase();

            EditorUtility.DisplayDialog("Success", $"{createdCount} new relics created, {updatedCount} relics updated from CSV!", "OK");
        }

        public static void ImportRelicsFromDefaultCSVPath()
        {
            string path = "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicImportData.csv";
            if (!File.Exists(path))
            {
                Debug.LogError($"[RelicManager] CSV file not found at: {path}");
                return;
            }

            List<Dictionary<string, string>> data = DataSyncUtility.ImportFromCSV(path);
            if (data == null)
            {
                Debug.LogError("[RelicManager] Failed to import data from CSV.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:RelicData");
            List<RelicData> allRelicsList = new List<RelicData>();
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                RelicData relicAsset = AssetDatabase.LoadAssetAtPath<RelicData>(assetPath);
                if (relicAsset != null)
                {
                    allRelicsList.Add(relicAsset);
                }
            }

            int updatedCount = 0;
            int createdCount = 0;

            foreach (Dictionary<string, string> entry in data)
            {
                if (!entry.ContainsKey("EN_Name") || string.IsNullOrEmpty(entry["EN_Name"])) continue;

                string enName = entry["EN_Name"];
                RelicData relic = allRelicsList.Find(r => r.relicName == enName);
                bool isNew = (relic == null);

                if (isNew)
                {
                    SimpleStatRelicData newRelic = ScriptableObject.CreateInstance<SimpleStatRelicData>();
                    newRelic.relicName = enName;
                    relic = newRelic;
                }

                Undo.RecordObject(relic, "Update Relic from CSV");

                if (entry.ContainsKey("KR_Name")) relic.koreanName = entry["KR_Name"];
                if (entry.ContainsKey("Rarity"))
                {
                    if (System.Enum.TryParse(entry["Rarity"], out Rarity rarity))
                        relic.rarity = rarity;
                }
                if (entry.ContainsKey("Level"))
                {
                    if (int.TryParse(entry["Level"], out int level))
                        relic.level = level;
                }
                if (entry.ContainsKey("Description_KR")) relic.description_KR = entry["Description_KR"];
                if (entry.ContainsKey("Synergy_Series_ID")) relic.synergySeriesId = entry["Synergy_Series_ID"];
                if (entry.ContainsKey("Unlock_Milestone_ID")) relic.unlockMilestoneID = entry["Unlock_Milestone_ID"];

                if (ImportInfluenceZones(entry, relic))
                    RelicInfluencePolicy.Normalize(relic);

                if (relic is SimpleStatRelicData simpleRelic)
                {
                    if (simpleRelic.simpleStatModifiers == null)
                    {
                        simpleRelic.simpleStatModifiers = new List<StatModifier>();
                    }
                    simpleRelic.simpleStatModifiers.Clear();

                    if (entry.ContainsKey("Stat1_Type") && !string.IsNullOrEmpty(entry["Stat1_Type"]))
                    {
                        if (System.Enum.TryParse(entry["Stat1_Type"], out StatType stat1Type) && !stat1Type.IsRetired())
                        {
                            float val = 0f;
                            float valPerLvl = 0f;
                            bool isPct = false;

                            if (entry.ContainsKey("Stat1_Value")) float.TryParse(entry["Stat1_Value"], out val);
                            if (entry.ContainsKey("Stat1_ValuePerLevel")) float.TryParse(entry["Stat1_ValuePerLevel"], out valPerLvl);
                            if (entry.ContainsKey("Stat1_IsPercentage"))
                            {
                                string isPctStr = entry["Stat1_IsPercentage"].ToLower();
                                isPct = (isPctStr == "true" || isPctStr == "1");
                            }

                            StatModifier modifier1 = new StatModifier
                            {
                                stat = stat1Type,
                                value = val,
                                valuePerLevel = valPerLvl,
                                isPercentage = isPct
                            };
                            simpleRelic.simpleStatModifiers.Add(modifier1);
                        }
                    }

                    if (entry.ContainsKey("Stat2_Type") && !string.IsNullOrEmpty(entry["Stat2_Type"]))
                    {
                        if (System.Enum.TryParse(entry["Stat2_Type"], out StatType stat2Type) && !stat2Type.IsRetired())
                        {
                            float val = 0f;
                            float valPerLvl = 0f;
                            bool isPct = false;

                            if (entry.ContainsKey("Stat2_Value")) float.TryParse(entry["Stat2_Value"], out val);
                            if (entry.ContainsKey("Stat2_ValuePerLevel")) float.TryParse(entry["Stat2_ValuePerLevel"], out valPerLvl);
                            if (entry.ContainsKey("Stat2_IsPercentage"))
                            {
                                string isPctStr = entry["Stat2_IsPercentage"].ToLower();
                                isPct = (isPctStr == "true" || isPctStr == "1");
                            }

                            StatModifier modifier2 = new StatModifier
                            {
                                stat = stat2Type,
                                value = val,
                                valuePerLevel = valPerLvl,
                                isPercentage = isPct
                            };
                            simpleRelic.simpleStatModifiers.Add(modifier2);
                        }
                    }

                    simpleRelic.InitializeSimpleStats(true);
                }

                EditorUtility.SetDirty(relic);

                if (isNew)
                {
                    string directoryPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats";
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    string assetPath = $"{directoryPath}/{enName.Replace(" ", "_")}.asset";
                    AssetDatabase.CreateAsset(relic, assetPath);

                    AddToDatabase(relic);
                    AddToGachaPool(relic);
                    createdCount++;
                    allRelicsList.Add(relic);
                }
                else
                {
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            SyncAllToDatabase(allRelicsList);

            Debug.Log($"[RelicManager] Batch import success: {createdCount} relics created, {updatedCount} relics updated.");
        }
    }

    public class RelicAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool databaseChanged = false;
            RelicDatabaseSO database = null;

            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".asset"))
                {
                    RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(str);
                    if (relic != null)
                    {
                        if (database == null) database = GetDatabase();
                        if (database != null)
                        {
                            if (database.allRelics == null) database.allRelics = new List<RelicData>();
                            if (!database.allRelics.Contains(relic))
                            {
                                database.allRelics.Add(relic);
                                databaseChanged = true;
                            }
                        }
                    }
                }
            }

            if (databaseChanged && database != null)
            {
                database.allRelics.RemoveAll(r => r == null);
                database.allRelics = database.allRelics.OrderBy(r => r.koreanName).ToList();
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Debug.Log("[RelicManager] Database automatically updated via AssetPostprocessor.");
            }

            // 가챠 풀 자동 동기화 호출
            RelicGachaSync.SyncRelicsToGachaPools(false);
        }

        private static RelicDatabaseSO GetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:RelicDatabaseSO");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }
    }
}
