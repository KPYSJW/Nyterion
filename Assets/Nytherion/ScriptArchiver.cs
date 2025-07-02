// ScriptArchiver.cs 파일에 이 코드를 붙여넣거나 업데이트해줘.
using UnityEngine;
using UnityEditor;
using System.IO;
using System.IO.Compression;

public class ScriptArchiver
{
    [MenuItem("Tools/Scripts/Archive Scripts in Folder")]
    private static void ArchiveScripts()
    {
        // 1. 유저에게 어떤 폴더를 압축할지 물어보기
        string sourceFolderPath = EditorUtility.OpenFolderPanel("압축할 스크립트 폴더 선택", "Assets", "");

        if (string.IsNullOrEmpty(sourceFolderPath)) return;

        // 2. 해당 폴더와 모든 하위 폴더에서 .cs 파일 찾기
        string[] scriptPaths = Directory.GetFiles(sourceFolderPath, "*.cs", SearchOption.AllDirectories);

        if (scriptPaths.Length == 0)
        {
            EditorUtility.DisplayDialog("알림", "선택한 폴더에 스크립트 파일(.cs)이 없습니다.", "확인");
            return;
        }

        // 3. ✨ 유저에게 압축 방식을 물어보는 선택창 띄우기 ✨
        int option = EditorUtility.DisplayDialogComplex(
            "압축 방식 선택",
            "스크립트 파일들을 어떻게 압축할까요?",
            "폴더 구조 유지",      // 선택지 0번
            "파일만 모아서 압축",  // 선택지 1번
            "취소"                 // 선택지 2번
        );

        // 선택에 따라 압축 방식 결정 (취소하면 작업 중단)
        bool preserveStructure;
        switch (option)
        {
            case 0: // 폴더 구조 유지
                preserveStructure = true;
                break;
            case 1: // 파일만 압축
                preserveStructure = false;
                break;
            default: // 취소 또는 창 닫기
                return;
        }

        // 4. 압축 파일을 어디에 저장할지 물어보기
        string savePath = EditorUtility.SaveFilePanel("압축 파일 저장 위치 선택", "", "ScriptsArchive", "zip");

        if (string.IsNullOrEmpty(savePath)) return;

        // 5. 찾은 스크립트 파일들을 선택한 방식으로 ZIP 파일 압축하기
        using (FileStream zipToCreate = new FileStream(savePath, FileMode.Create))
        {
            using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
            {
                foreach (string scriptPath in scriptPaths)
                {
                    string entryName;
                    if (preserveStructure)
                    {
                        // "폴더 구조 유지"를 선택한 경우: 상대 경로를 그대로 사용
                        entryName = scriptPath.Substring(sourceFolderPath.Length + 1);
                    }
                    else
                    {
                        // "파일만 모아서 압축"을 선택한 경우: 파일 이름만 사용
                        entryName = Path.GetFileName(scriptPath);
                    }

                    // 파일을 압축 아카이브에 추가
                    archive.CreateEntryFromFile(scriptPath, entryName);
                }
            }
        }

        // 6. 작업 완료!
        EditorUtility.DisplayDialog("성공!", $"총 {scriptPaths.Length}개의 스크립트 파일을 성공적으로 압축했어!\n저장 위치: {savePath}", "확인");
        EditorUtility.RevealInFinder(savePath);
    }
}