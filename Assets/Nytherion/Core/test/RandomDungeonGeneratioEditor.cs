using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AbstractDungeonGenertor),true)]

public class RandomDungeonGeneratioEditor : Editor
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
