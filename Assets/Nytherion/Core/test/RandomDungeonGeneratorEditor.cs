using UnityEditor;
using UnityEngine;
using Nytherion.GamePlay.Dungeon;

[CustomEditor(typeof(AbstractDungeonGenertor),true)]

public class RandomDungeonGeneratorEditor : Editor
{
    AbstractDungeonGenertor generator;

    private void Awake()
    {
        generator = (AbstractDungeonGenertor)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Generate Dungeon"))
        {
            generator.GenerateDungeon();
        }
    }
}
