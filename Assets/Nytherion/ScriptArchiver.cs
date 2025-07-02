using UnityEngine;
using UnityEditor;
using System.IO;
using System.IO.Compression;

public class ScriptArchiver
{
    [MenuItem("Tools/Scripts/Archive Scripts in Folder")]
    private static void ArchiveScripts()
    {
        string sourceFolderPath = EditorUtility.OpenFolderPanel("압축할 스크립트 폴더 선택", "Assets", "");

        if (string.IsNullOrEmpty(sourceFolderPath)) return;

        string[] scriptPaths = Directory.GetFiles(sourceFolderPath, "*.cs", SearchOption.AllDirectories);

        if (scriptPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("알림", "선택한 폴더에 스크립트 파일(.cs)이 없습니다.", "확인");
            return;
        }

        int option = EditorUtility.DisplayDialogComplex(
            "압축 방식 선택",
            "스크립트 파일들을 어떻게 압축할까요?",
            "폴더 구조 유지",      
            "파일만 모아서 압축",  
            "취소"                 
        );

        bool preserveStructure;
        switch (option)
        {
            case 0: 
                preserveStructure = true;
                break;
            case 1: 
                preserveStructure = false;
                break;
            default: 
                return;
        }

        string savePath = EditorUtility.SaveFilePanel("압축 파일 저장 위치 선택", "", "ScriptsArchive", "zip");

        if (string.IsNullOrEmpty(savePath)) return;

        using (FileStream zipToCreate = new FileStream(savePath, FileMode.Create))
        {
            using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
            {
                foreach (string scriptPath in scriptPaths)
                {
                    string entryName;
                    if (preserveStructure)
                    {
                        
                        entryName = scriptPath.Substring(sourceFolderPath.Length + 1);
                    }
                    else
                    {
                        
                        entryName = Path.GetFileName(scriptPath);
                    }

                    
                    archive.CreateEntryFromFile(scriptPath, entryName);
                }
            }
        }

        
        EditorUtility.DisplayDialog("성공!", $"총 {scriptPaths.Length}개의 스크립트 파일을 성공적으로 압축했어!\n저장 위치: {savePath}", "확인");
        EditorUtility.RevealInFinder(savePath);
    }
}