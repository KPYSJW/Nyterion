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

        // Influence Grid Data (3x3 buffer)
        private InfluenceType[,] influenceGrid = new InfluenceType[3, 3];
        
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
            ResetInfluenceGrid();
            InitializeCreationData();
        }

        private void InitializeCreationData()
        {
            creationData = ScriptableObject.CreateInstance<RelicData>();
            serializedCreationData = new SerializedObject(creationData);
            creationEffectModulesProp = serializedCreationData.FindProperty("effectModules");
        }

        private void ResetInfluenceGrid()
        {
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    influenceGrid[x, y] = InfluenceType.None;
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
                if (currentTab == WindowTab.Edit) RefreshRelicList();
                lastTab = currentTab;
            }

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
            relicName = EditorGUILayout.TextField("Name (EN/ID)", relicName);
            koreanName = EditorGUILayout.TextField("Name (KR)", koreanName);
            
            GUILayout.Label("Description (KR)");
            description_KR = EditorGUILayout.TextArea(description_KR, GUILayout.Height(60));
            GUILayout.Label("Description (EN)");
            description_EN = EditorGUILayout.TextArea(description_EN, GUILayout.Height(60));

            rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", rarity);
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
        }

        private RelicDatabaseSO GetDatabase()
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
            RelicDatabaseSO database = GetDatabase();
            if (database == null)
            {
                Debug.LogError("[RelicManager] Failed to find RelicDatabaseSO.");
                return;
            }

            Undo.RecordObject(database, "Sync Relic Database");
            if (database.allRelics == null) database.allRelics = new List<RelicData>();

            int addedCount = 0;
            foreach (var relic in allRelics)
            {
                if (!database.allRelics.Contains(relic))
                {
                    database.allRelics.Add(relic);
                    addedCount++;
                }
            }

            // Remove nulls and sort by name
            database.allRelics.RemoveAll(r => r == null);
            database.allRelics = database.allRelics.OrderBy(r => r.koreanName).ToList();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            if (addedCount > 0)
                Debug.Log($"[RelicManager] Database synced. {addedCount} relics added.");
        }

        private void LoadRelicToGrid(RelicData relic)
        {
            ResetInfluenceGrid();
            if (relic.influenceZones == null) return;

            foreach (var zone in relic.influenceZones)
            {
                int x = zone.offset.x + 1;
                int y = zone.offset.y + 1;
                if (x >= 0 && x < 3 && y >= 0 && y < 3)
                {
                    influenceGrid[x, y] = zone.type;
                }
            }
        }

        private void SyncGridToRelic(RelicData relic)
        {
            if (relic.influenceZones == null) relic.influenceZones = new List<InfluenceZone>();
            relic.influenceZones.Clear();

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (x == 1 && y == 1) continue;
                    if (influenceGrid[x, y] != InfluenceType.None)
                    {
                        relic.influenceZones.Add(new InfluenceZone
                        {
                            offset = new Vector2Int(x - 1, y - 1),
                            type = influenceGrid[x, y]
                        });
                    }
                }
            }
            EditorUtility.SetDirty(relic);
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
            selectedRelic.relicName = EditorGUILayout.TextField("Name (EN)", selectedRelic.relicName);
            
            GUILayout.Label("Description (KR)");
            selectedRelic.description_KR = EditorGUILayout.TextArea(selectedRelic.description_KR, GUILayout.Height(60));
            GUILayout.Label("Description (EN)");
            selectedRelic.description_EN = EditorGUILayout.TextArea(selectedRelic.description_EN, GUILayout.Height(60));
            
            EditorGUILayout.Space(5);
            selectedRelic.rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", selectedRelic.rarity);
            selectedRelic.level = EditorGUILayout.IntField("Level", selectedRelic.level);
            selectedRelic.Image = (Sprite)EditorGUILayout.ObjectField("Image", selectedRelic.Image, typeof(Sprite), false);
            EditorGUILayout.EndVertical();

            // Right: Grid Editor
            EditorGUILayout.BeginVertical();
            DrawInfluenceGridEditor(selectedRelic);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

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
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Influence Zones (Visual Editor)", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Click cells to cycle: None -> Up -> Down -> Silence", MessageType.Info);

            EditorGUILayout.Space(5);

            // Center the grid
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            for (int y = 2; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < 3; x++)
                {
                    bool isCenter = (x == 1 && y == 1);
                    
                    if (isCenter)
                    {
                        GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                        GUILayout.Button("RELIC", GUILayout.Width(60), GUILayout.Height(60));
                    }
                    else
                    {
                        InfluenceType type = influenceGrid[x, y];
                        SetGridColor(type);

                        string label = type == InfluenceType.None ? "" : type.ToString().Replace("Level", "");
                        if (GUILayout.Button(label, GUILayout.Width(60), GUILayout.Height(60)))
                        {
                            CycleInfluenceType(x, y);
                            if (targetRelic != null) SyncGridToRelic(targetRelic);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    if (x < 2) GUILayout.Space(2);
                }
                EditorGUILayout.EndHorizontal();
                if (y > 0) GUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Grid", GUILayout.Width(100)))
            {
                ResetInfluenceGrid();
                if (targetRelic != null) SyncGridToRelic(targetRelic);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
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

        private void CycleInfluenceType(int x, int y)
        {
            influenceGrid[x, y] = (InfluenceType)(((int)influenceGrid[x, y] + 1) % 4);
        }

        private void CreateRelicData()
        {
            if (string.IsNullOrEmpty(relicName)) { EditorUtility.DisplayDialog("Error", "Please enter an English name.", "OK"); return; }

            RelicData newData = ScriptableObject.CreateInstance<RelicData>();
            newData.relicName = relicName;
            newData.koreanName = koreanName;
            newData.description_KR = description_KR;
            newData.description_EN = description_EN;
            newData.Image = relicImage;
            newData.rarity = rarity;
            newData.level = level;

            // Copy Effect Modules from temporary creationData
            if (creationData.effectModules != null)
            {
                newData.effectModules = new List<Nytherion.Gameplay.Relics.Modules.RelicEffectModule>(creationData.effectModules);
            }

            // Convert Grid to InfluenceZones
            newData.influenceZones = new List<InfluenceZone>();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (x == 1 && y == 1) continue;
                    if (influenceGrid[x, y] != InfluenceType.None)
                    {
                        newData.influenceZones.Add(new InfluenceZone
                        {
                            offset = new Vector2Int(x - 1, y - 1),
                            type = influenceGrid[x, y]
                        });
                    }
                }
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
                    rewardType = RewardType.Relic,
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
            RefreshRelicList();
            Selection.activeObject = newData;
            Debug.Log($"[RelicManager] Created relic: {fullPath}");

            // Reset Creation Fields
            relicName = "NewRelic";
            koreanName = "새 유물";
            description_KR = "";
            description_EN = "";
            relicImage = null;
            createMilestone = false;
            milestoneTitle = "유물 해금: ";
            ResetInfluenceGrid();
            InitializeCreationData(); // Reset effect modules
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

        private void AddToDatabase(RelicData newRelic)
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

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Relics to CSV", "", "RelicData.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            string[] headers = new string[] { "EN_Name", "KR_Name", "Rarity", "Level", "Description_KR" };
            List<string[]> rows = new List<string[]>();

            foreach (RelicData r in allRelics)
            {
                rows.Add(new string[] {
                    r.relicName,
                    r.koreanName,
                    r.rarity.ToString(),
                    r.level.ToString(),
                    r.description_KR
                });
            }

            DataSyncUtility.ExportToCSV(path, headers, rows);
            EditorUtility.DisplayDialog("Success", "Relics exported successfully!", "OK");
        }

        private void ImportCSV()
        {
            string path = EditorUtility.OpenFilePanel("Import Relics from CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            List<Dictionary<string, string>> data = DataSyncUtility.ImportFromCSV(path);
            if (data == null) return;

            int updatedCount = 0;
            foreach (Dictionary<string, string> entry in data)
            {
                string enName = entry["EN_Name"];
                RelicData relic = allRelics.Find(r => r.relicName == enName);

                if (relic != null)
                {
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

                    EditorUtility.SetDirty(relic);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            RefreshRelicList();
            EditorUtility.DisplayDialog("Success", $"{updatedCount} relics updated from CSV!", "OK");
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
