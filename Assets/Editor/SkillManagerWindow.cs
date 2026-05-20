using UnityEditor;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Progression;
using Nytherion.Core.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace Nytherion.Editor
{
    public class SkillManagerWindow : EditorWindow
    {
        private const string SKILL_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Skill";
        private const string DATABASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset";
        private const string MILESTONE_DATA_PATH = "Assets/Nytherion/Data/ScriptableObjects/Progression";

        private enum WindowTab { Create, Edit }
        private WindowTab currentTab = WindowTab.Create;

        private SkillData creationData;
        private SerializedObject serializedCreationData;
        private int selectedSkillTypeIndex = 0;
        private List<Type> skillTypes = new List<Type>();
        private string[] skillTypeNames;

        private bool createMilestone = false;
        private string milestoneTitle = "스킬 해금: ";
        private ProgressionType milestoneType = ProgressionType.KillEnemy;
        private int milestoneTarget = 100;

        private List<SkillData> allSkills = new List<SkillData>();
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private SkillData selectedSkill;
        private SerializedObject serializedSkill;
        private WindowTab lastTab = WindowTab.Create;

        [MenuItem("Nytherion/Skill Manager")]
        public static void ShowWindow()
        {
            GetWindow<SkillManagerWindow>("Skill Manager");
        }

        private void OnEnable()
        {
            InitializeSkillTypes();
            RefreshSkillList();
            InitializeCreationData();
        }

        private void InitializeSkillTypes()
        {
            skillTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(SkillData).IsAssignableFrom(p) && !p.IsAbstract)
                .OrderBy(t => t.Name)
                .ToList();
            
            skillTypeNames = skillTypes.Select(t => t.Name).ToArray();
        }

        private void InitializeCreationData()
        {
            if (skillTypes.Count > 0)
            {
                creationData = (SkillData)CreateInstance(skillTypes[selectedSkillTypeIndex]);
                serializedCreationData = new SerializedObject(creationData);
            }
        }

        private void RefreshSkillList()
        {
            AssetDatabase.Refresh();
            allSkills = AssetDatabase.FindAssets("t:SkillData")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(s => s != null)
                .OrderBy(s => s.skillName)
                .ToList();
        }

        private void OnGUI()
        {
            currentTab = (WindowTab)GUILayout.Toolbar((int)currentTab, new string[] { "Create New", "Edit All" });
            
            if (currentTab != lastTab)
            {
                if (currentTab == WindowTab.Edit) RefreshSkillList();
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
            
            GUILayout.Label("Skill Creation Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            int newTypeIndex = EditorGUILayout.Popup("Skill Type", selectedSkillTypeIndex, skillTypeNames);
            if (newTypeIndex != selectedSkillTypeIndex)
            {
                selectedSkillTypeIndex = newTypeIndex;
                InitializeCreationData();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            if (serializedCreationData != null)
            {
                serializedCreationData.Update();
                
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Skill Properties", EditorStyles.miniBoldLabel);
                
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
                
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Unlock Achievement (Milestone)", EditorStyles.miniBoldLabel);
                createMilestone = EditorGUILayout.Toggle("Create Unlock Milestone?", createMilestone);
                if (createMilestone)
                {
                    milestoneTitle = EditorGUILayout.TextField("Milestone Title", milestoneTitle);
                    milestoneType = (ProgressionType)EditorGUILayout.EnumPopup("Unlock Action", milestoneType);
                    milestoneTarget = EditorGUILayout.IntField("Target Value", milestoneTarget);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
                if (GUILayout.Button("Create Skill Data", GUILayout.Height(40))) CreateSkillData();
                
                serializedCreationData.ApplyModifiedProperties();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEditTab()
        {
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshSkillList();
            if (GUILayout.Button("Sync DB", GUILayout.Width(80))) SyncAllToDatabase();

            GUI.color = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button("Export CSV", GUILayout.Width(100))) ExportCSV();
            GUI.color = new Color(1f, 0.8f, 0.8f);
            if (GUILayout.Button("Import CSV", GUILayout.Width(100))) ImportCSV();
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            CheckDatabaseSyncStatus();

            float listHeight = selectedSkill == null ? position.height - 120 : position.height * 0.4f;
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight));
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Name", GUILayout.Width(150));
            GUILayout.Label("ID", GUILayout.Width(150));
            GUILayout.Label("Type", GUILayout.Width(100));
            GUILayout.Label("Actions", GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            foreach (var skill in allSkills)
            {
                if (!string.IsNullOrEmpty(searchFilter) && 
                    !skill.skillName.Contains(searchFilter) && 
                    !skill.skillID.ToLower().Contains(searchFilter.ToLower()))
                    continue;

                bool isSelected = selectedSkill == skill;
                GUI.backgroundColor = isSelected ? new Color(0.7f, 0.7f, 1f) : Color.white;
                
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(skill.skillName, GUILayout.Width(150));
                GUILayout.Label(skill.skillID, GUILayout.Width(150));
                GUILayout.Label(skill.skillType.ToString(), GUILayout.Width(100));

                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                {
                    selectedSkill = skill;
                    serializedSkill = new SerializedObject(selectedSkill);
                }
                if (GUILayout.Button("View", GUILayout.Width(50))) Selection.activeObject = skill;
                
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();

            if (selectedSkill != null) DrawSelectedSkillPanel();

            if (GUILayout.Button("Save All Changes", GUILayout.Height(30)))
            {
                AssetDatabase.SaveAssets();
                SyncAllToDatabase();
                Debug.Log("[SkillManager] All changes saved and database synced.");
            }
        }

        private void DrawSelectedSkillPanel()
        {
            if (serializedSkill == null || serializedSkill.targetObject != selectedSkill)
            {
                serializedSkill = new SerializedObject(selectedSkill);
            }

            serializedSkill.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Detailed Editing: {selectedSkill.skillName}", EditorStyles.boldLabel);
            if (GUILayout.Button("Close", GUILayout.Width(60))) 
            {
                selectedSkill = null;
                serializedSkill = null;
                return;
            }
            EditorGUILayout.EndHorizontal();

            Vector2 detailScroll = Vector2.zero;
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MaxHeight(600));

            SerializedProperty prop = serializedSkill.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_Script") continue;
                    EditorGUILayout.PropertyField(prop, true);
                }
                while (prop.NextVisible(false));
            }

            if (serializedSkill.ApplyModifiedProperties() || GUI.changed) 
            {
                EditorUtility.SetDirty(selectedSkill);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void CheckDatabaseSyncStatus()
        {
            SkillDatabaseSO database = GetDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("Skill Database not found!", MessageType.Error);
                return;
            }

            int missingCount = allSkills.Count(s => !database.allSkills.Contains(s));
            if (missingCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = Color.yellow;
                EditorGUILayout.HelpBox($"{missingCount} skills are missing from the database.", MessageType.Warning);
                GUI.color = Color.white;
                if (GUILayout.Button("Fix Now", GUILayout.Height(38)))
                {
                    SyncAllToDatabase();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private SkillDatabaseSO GetDatabase()
        {
            SkillDatabaseSO database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(DATABASE_PATH);
            if (database == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SkillDatabaseSO");
                if (guids.Length > 0)
                {
                    database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            return database;
        }

        private void SyncAllToDatabase()
        {
            SkillDatabaseSO database = GetDatabase();
            if (database == null)
            {
                Debug.LogError("[SkillManager] Failed to find SkillDatabaseSO.");
                return;
            }

            Undo.RecordObject(database, "Sync Skill Database");
            if (database.allSkills == null) database.allSkills = new List<SkillData>();

            int addedCount = 0;
            foreach (var skill in allSkills)
            {
                if (!database.allSkills.Contains(skill))
                {
                    database.allSkills.Add(skill);
                    addedCount++;
                }
            }

            database.allSkills.RemoveAll(s => s == null);
            database.allSkills = database.allSkills.OrderBy(s => s.skillName).ToList();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            if (addedCount > 0)
                Debug.Log($"[SkillManager] Database synced. {addedCount} skills added.");
        }

        private void CreateSkillData()
        {
            if (string.IsNullOrEmpty(creationData.skillID)) 
            { 
                EditorUtility.DisplayDialog("Error", "Please enter a Skill ID.", "OK"); 
                return; 
            }

            string fullPath = $"{SKILL_DATA_PATH}/{creationData.skillID.Replace(" ", "_")}.asset";
            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("Warning", "File already exists. Overwrite?", "Yes", "No"))
                    return;
            }

            SkillData newData = Instantiate(creationData);
            
            if (!Directory.Exists(SKILL_DATA_PATH)) Directory.CreateDirectory(SKILL_DATA_PATH);
            
            if (createMilestone)
            {
                string mID = "UNLOCK_SKILL_" + newData.skillID.ToUpper();
                newData.unlockMilestoneID = mID;

                MilestoneData newMilestone = CreateInstance<MilestoneData>();
                newMilestone.milestoneID = mID;
                newMilestone.title = milestoneTitle;
                newMilestone.description = $"{newData.skillName} 스킬을 해금하기 위한 업적입니다.";
                newMilestone.progressionType = milestoneType;
                newMilestone.targetValue = milestoneTarget;
                newMilestone.icon = newData.icon;

                RewardData reward = new RewardData
                {
                    rewardType = RewardType.Skill,
                    skillData = newData,
                    amount = 1
                };
                newMilestone.rewards = new List<RewardData> { reward };

                if (!Directory.Exists(MILESTONE_DATA_PATH)) Directory.CreateDirectory(MILESTONE_DATA_PATH);
                string mPath = $"{MILESTONE_DATA_PATH}/{mID}.asset";
                AssetDatabase.CreateAsset(newMilestone, mPath);
                
                AddMilestoneToDatabase(newMilestone);
                Debug.Log($"[SkillManager] Automatically created milestone: {mPath}");
            }

            AssetDatabase.CreateAsset(newData, fullPath);
            AssetDatabase.SaveAssets();

            AddToDatabase(newData);
            RefreshSkillList();
            Selection.activeObject = newData;
            Debug.Log($"[SkillManager] Created skill: {fullPath}");

            InitializeCreationData(); // Reset for next creation
            createMilestone = false;
            milestoneTitle = "스킬 해금: ";
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

        private void AddToDatabase(SkillData newSkill)
        {
            SkillDatabaseSO database = GetDatabase();
            if (database != null)
            {
                if (database.allSkills == null) database.allSkills = new List<SkillData>();
                if (!database.allSkills.Contains(newSkill))
                {
                    Undo.RecordObject(database, "Add Skill to Database");
                    database.allSkills.Add(newSkill);
                    database.allSkills = database.allSkills.OrderBy(s => s.skillName).ToList();
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[SkillManager] Automatically registered new skill: {newSkill.skillName}");
                }
            }
        }

        private void ExportCSV()
        {
            string path = EditorUtility.SaveFilePanel("Export Skills to CSV", "", "SkillData.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            string[] headers = new string[] { "ID", "Name", "Type", "Cooldown", "ManaCost", "Description" };
            List<string[]> rows = new List<string[]>();

            foreach (SkillData s in allSkills)
            {
                rows.Add(new string[] {
                    s.skillID,
                    s.skillName,
                    s.skillType.ToString(),
                    s.coolDown.ToString(),
                    s.manaCost.ToString(),
                    s.description
                });
            }

            DataSyncUtility.ExportToCSV(path, headers, rows);
            EditorUtility.DisplayDialog("Success", "Skills exported successfully!", "OK");
        }

        private void ImportCSV()
        {
            string path = EditorUtility.OpenFilePanel("Import Skills from CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            List<Dictionary<string, string>> data = DataSyncUtility.ImportFromCSV(path);
            if (data == null) return;

            int updatedCount = 0;
            foreach (Dictionary<string, string> entry in data)
            {
                string id = entry["ID"];
                SkillData skill = allSkills.Find(s => s.skillID == id);

                if (skill != null)
                {
                    Undo.RecordObject(skill, "Update Skill from CSV");
                    if (entry.ContainsKey("Name")) skill.skillName = entry["Name"];
                    if (entry.ContainsKey("Cooldown"))
                    {
                        if (float.TryParse(entry["Cooldown"], out float cd)) skill.coolDown = cd;
                    }
                    if (entry.ContainsKey("ManaCost"))
                    {
                        if (int.TryParse(entry["ManaCost"], out int mana)) skill.manaCost = mana;
                    }
                    if (entry.ContainsKey("Description")) skill.description = entry["Description"];

                    EditorUtility.SetDirty(skill);
                    updatedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            RefreshSkillList();
            EditorUtility.DisplayDialog("Success", $"{updatedCount} skills updated from CSV!", "OK");
        }
    }

    public class SkillAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool databaseChanged = false;
            SkillDatabaseSO database = null;

            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".asset"))
                {
                    SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(str);
                    if (skill != null)
                    {
                        if (database == null) database = GetDatabase();
                        if (database != null)
                        {
                            if (database.allSkills == null) database.allSkills = new List<SkillData>();
                            if (!database.allSkills.Contains(skill))
                            {
                                database.allSkills.Add(skill);
                                databaseChanged = true;
                            }
                        }
                    }
                }
            }

            if (databaseChanged && database != null)
            {
                database.allSkills.RemoveAll(s => s == null);
                database.allSkills = database.allSkills.OrderBy(s => s.skillName).ToList();
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Debug.Log("[SkillManager] Skill database automatically updated.");
            }
        }

        private static SkillDatabaseSO GetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillDatabaseSO");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }
    }
}
