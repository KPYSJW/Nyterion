using UnityEditor;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Progression;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nytherion.Editor
{
    public class MilestoneManagerWindow : EditorWindow
    {
        private const string MILESTONE_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Progression";
        private const string DATABASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Progression/MilestoneDatabase.asset";

        private enum WindowTab { Create, Edit }
        private WindowTab currentTab = WindowTab.Create;

        private MilestoneData creationData;
        private SerializedObject serializedCreationData;

        private List<MilestoneData> allMilestones = new List<MilestoneData>();
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private MilestoneData selectedMilestone;
        private SerializedObject serializedMilestone;
        private WindowTab lastTab = WindowTab.Create;

        [MenuItem("Nytherion/Milestone Manager")]
        public static void ShowWindow()
        {
            GetWindow<MilestoneManagerWindow>("Milestone Manager");
        }

        private void OnEnable()
        {
            RefreshMilestoneList();
            InitializeCreationData();
        }

        private void InitializeCreationData()
        {
            creationData = ScriptableObject.CreateInstance<MilestoneData>();
            serializedCreationData = new SerializedObject(creationData);
        }

        private void RefreshMilestoneList()
        {
            AssetDatabase.Refresh();
            allMilestones = AssetDatabase.FindAssets("t:MilestoneData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<MilestoneData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(m => m != null)
                .OrderBy(m => m.milestoneID)
                .ToList();
        }

        private void OnGUI()
        {
            currentTab = (WindowTab)GUILayout.Toolbar((int)currentTab, new string[] { "Create New", "Edit All" });
            
            if (currentTab != lastTab)
            {
                if (currentTab == WindowTab.Edit) RefreshMilestoneList();
                lastTab = currentTab;
            }

            EditorGUILayout.Space();

            if (currentTab == WindowTab.Create) DrawCreateTab();
            else DrawEditTab();
        }

        private void DrawCreateTab()
        {
            if (serializedCreationData == null) InitializeCreationData();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Milestone Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            serializedCreationData.Update();
            
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Milestone Properties", EditorStyles.miniBoldLabel);
            
            SerializedProperty prop = serializedCreationData.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_Script") continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                while (prop.NextVisible(false));
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Milestone Data", GUILayout.Height(40))) CreateMilestoneData();
            
            serializedCreationData.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawEditTab()
        {
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshMilestoneList();
            if (GUILayout.Button("Sync DB", GUILayout.Width(80))) SyncAllToDatabase();
            
            GUI.color = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Export CSV", GUILayout.Width(100))) ExportCSV();
            GUI.color = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Import CSV", GUILayout.Width(100))) ImportCSV();
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            CheckDatabaseSyncStatus();

            float listHeight = selectedMilestone == null ? position.height - 120 : position.height * 0.4f;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight));
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("ID", GUILayout.Width(150));
            GUILayout.Label("Title", GUILayout.Width(200));
            GUILayout.Label("Target", GUILayout.Width(60));
            GUILayout.Label("Actions", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            foreach (var milestone in allMilestones)
            {
                if (!string.IsNullOrEmpty(searchFilter) && 
                    !milestone.milestoneID.ToLower().Contains(searchFilter.ToLower()) && 
                    !milestone.title.Contains(searchFilter))
                    continue;

                bool isSelected = selectedMilestone == milestone;
                GUI.backgroundColor = isSelected ? new Color(0.7f, 0.7f, 1f) : Color.white;
                
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(milestone.milestoneID, GUILayout.Width(150));
                GUILayout.Label(milestone.title, GUILayout.Width(200));
                GUILayout.Label(milestone.targetValue.ToString(), GUILayout.Width(60));

                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                {
                    selectedMilestone = milestone;
                    serializedMilestone = new SerializedObject(selectedMilestone);
                }
                if (GUILayout.Button("View", GUILayout.Width(50))) Selection.activeObject = milestone;
                
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            if (selectedMilestone != null) DrawSelectedMilestonePanel();

            if (GUILayout.Button("Save All Changes", GUILayout.Height(30)))
            {
                AssetDatabase.SaveAssets();
                SyncAllToDatabase();
                Debug.Log("[MilestoneManager] All changes saved and database synced.");
            }
        }

        private void DrawSelectedMilestonePanel()
        {
            if (serializedMilestone == null || serializedMilestone.targetObject != selectedMilestone)
            {
                serializedMilestone = new SerializedObject(selectedMilestone);
            }

            serializedMilestone.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Detailed Editing: {selectedMilestone.milestoneID}", EditorStyles.boldLabel);
            if (GUILayout.Button("Close", GUILayout.Width(60))) 
            {
                selectedMilestone = null;
                serializedMilestone = null;
                return;
            }
            EditorGUILayout.EndHorizontal();

            Vector2 detailScroll = Vector2.zero;
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MaxHeight(600));

            SerializedProperty prop = serializedMilestone.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_Script") continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                while (prop.NextVisible(false));
            }

            if (serializedMilestone.ApplyModifiedProperties() || GUI.changed) 
            {
                EditorUtility.SetDirty(selectedMilestone);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void CheckDatabaseSyncStatus()
        {
            MilestoneDatabaseSO database = GetDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("Milestone Database not found!", MessageType.Error);
                return;
            }

            int missingCount = allMilestones.Count(m => !database.allMilestones.Contains(m));
            if (missingCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = Color.yellow;
                EditorGUILayout.HelpBox($"{missingCount} milestones are missing from the database.", MessageType.Warning);
                GUI.color = Color.white;
                if (GUILayout.Button("Fix Now", GUILayout.Height(38)))
                {
                    SyncAllToDatabase();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private MilestoneDatabaseSO GetDatabase()
        {
            MilestoneDatabaseSO database = AssetDatabase.LoadAssetAtPath<MilestoneDatabaseSO>(DATABASE_PATH);
            if (database == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:MilestoneDatabaseSO");
                if (guids.Length > 0)
                {
                    database = AssetDatabase.LoadAssetAtPath<MilestoneDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            return database;
        }

        private void SyncAllToDatabase()
        {
            MilestoneDatabaseSO database = GetDatabase();
            if (database == null)
            {
                Debug.LogError("[MilestoneManager] Failed to find MilestoneDatabaseSO.");
                return;
            }

            Undo.RecordObject(database, "Sync Milestone Database");
            if (database.allMilestones == null) database.allMilestones = new List<MilestoneData>();

            int addedCount = 0;
            foreach (var milestone in allMilestones)
            {
                if (!database.allMilestones.Contains(milestone))
                {
                    database.allMilestones.Add(milestone);
                    addedCount++;
                }
            }

            database.allMilestones.RemoveAll(m => m == null);
            database.allMilestones = database.allMilestones.OrderBy(m => m.milestoneID).ToList();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            if (addedCount > 0)
                Debug.Log($"[MilestoneManager] Database synced. {addedCount} milestones added.");
        }

        private void CreateMilestoneData()
        {
            if (string.IsNullOrEmpty(creationData.milestoneID)) 
            { 
                EditorUtility.DisplayDialog("Error", "Please enter a Milestone ID.", "OK"); 
                return; 
            }

            string fullPath = $"{MILESTONE_DATA_PATH}/{creationData.milestoneID.Replace(" ", "_")}.asset";
            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("Warning", "File already exists. Overwrite?", "Yes", "No"))
                    return;
            }

            MilestoneData newData = (MilestoneData)ScriptableObject.Instantiate(creationData);
            
            if (!Directory.Exists(MILESTONE_DATA_PATH)) Directory.CreateDirectory(MILESTONE_DATA_PATH);
            
            AssetDatabase.CreateAsset(newData, fullPath);
            AssetDatabase.SaveAssets();

            AddToDatabase(newData);
            RefreshMilestoneList();
            Selection.activeObject = newData;
            Debug.Log($"[MilestoneManager] Created milestone: {fullPath}");

            InitializeCreationData();
        }

        private void AddToDatabase(MilestoneData newMilestone)
        {
            MilestoneDatabaseSO database = GetDatabase();
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
                    Debug.Log($"[MilestoneManager] Automatically registered new milestone: {newMilestone.milestoneID}");
                }
            }
        }

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Milestones to CSV", "", "MilestoneData.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            string[] headers = new string[] { "ID", "Title", "Description", "Type", "Target" };
            List<string[]> rows = new List<string[]>();

            foreach (MilestoneData m in allMilestones)
            {
                rows.Add(new string[] {
                    m.milestoneID,
                    m.title,
                    m.description,
                    m.progressionType.ToString(),
                    m.targetValue.ToString()
                });
            }

            DataSyncUtility.ExportToCSV(path, headers, rows);
            EditorUtility.DisplayDialog("Success", "Milestones exported successfully!", "OK");
        }

        private void ImportCSV()
        {
            string path = EditorUtility.OpenFilePanel("Import Milestones from CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            List<Dictionary<string, string>> data = DataSyncUtility.ImportFromCSV(path);
            if (data == null) return;

            int updatedCount = 0;
            foreach (Dictionary<string, string> entry in data)
            {
                string id = entry["ID"];
                MilestoneData milestone = allMilestones.Find(m => m.milestoneID == id);

                if (milestone != null)
                {
                    Undo.RecordObject(milestone, "Update Milestone from CSV");
                    if (entry.ContainsKey("Title")) milestone.title = entry["Title"];
                    if (entry.ContainsKey("Description")) milestone.description = entry["Description"];
                    if (entry.ContainsKey("Type")) 
                    {
                        if (System.Enum.TryParse(entry["Type"], out Nytherion.Core.Enums.ProgressionType type))
                            milestone.progressionType = type;
                    }
                    if (entry.ContainsKey("Target"))
                    {
                        if (int.TryParse(entry["Target"], out int target))
                            milestone.targetValue = target;
                    }
                    EditorUtility.SetDirty(milestone);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            RefreshMilestoneList();
            EditorUtility.DisplayDialog("Success", $"{updatedCount} milestones updated from CSV!", "OK");
        }
    }

    public class MilestoneAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool databaseChanged = false;
            MilestoneDatabaseSO database = null;

            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".asset"))
                {
                    MilestoneData milestone = AssetDatabase.LoadAssetAtPath<MilestoneData>(str);
                    if (milestone != null)
                    {
                        if (database == null) database = GetDatabase();
                        if (database != null)
                        {
                            if (database.allMilestones == null) database.allMilestones = new List<MilestoneData>();
                            if (!database.allMilestones.Contains(milestone))
                            {
                                database.allMilestones.Add(milestone);
                                databaseChanged = true;
                            }
                        }
                    }
                }
            }

            if (databaseChanged && database != null)
            {
                database.allMilestones.RemoveAll(m => m == null);
                database.allMilestones = database.allMilestones.OrderBy(m => m.milestoneID).ToList();
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Debug.Log("[MilestoneManager] Milestone database automatically updated.");
            }
        }

        private static MilestoneDatabaseSO GetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:MilestoneDatabaseSO");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<MilestoneDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }
    }
}
