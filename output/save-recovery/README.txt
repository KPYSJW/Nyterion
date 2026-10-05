저장 복구 수정 검증 — 2026-10-05

변경: JsonSaveService, SaveLoadManager, 개발용 GameManager 세이브 삭제 경로.
세이브 JSON 필드와 ID는 변경하지 않았다. JsonUtility 직렬화를 유지한다.
정상 파일은 검증한 임시 파일로 교체하고 직전 정상 파일을 .bak으로 보존한다.
원본이 손상된 경우 정상 백업으로 복구하며, 복구 후 첫 저장은 손상 원본을 .corrupt-*로 보관하고 기존 정상 백업을 유지한다.
복구 불가능하거나 읽기/데이터 적용이 실패하면 저장을 차단한다. 정상 파일을 복원하고 ForceLoadGame을 재실행하면 차단이 해제된다.
새 복구 UI는 추가하지 않았다. 오류는 로그, LastError, IsSaveBlocked, LastLoadStatus로 확인한다.

검증 결과:
- 전체 런타임 C# 및 Editor C# 컴파일 오류 0개. 기존 경고는 로그에 남아 있다.
- Unity 2022.3.62f2 격리 플레이 모드 회귀 검사 35개 통과, 실패 0개.
- 이전 저장 매니저의 강제 로드 후 영구 차단을 재현하고 수정 코드의 재저장을 확인했다.
- 실제 JsonUtility, 파일 I/O, MonoBehaviour, 초기화 코루틴, VContainer를 사용했다.
- 데이터 매니저 및 UI/스코프는 테스트 협력 객체다. 실제 GameScene/Village 통합 플레이와 배포 빌드는 확인하지 않았다.
- 실제 사용자 저장 파일은 읽거나 수정하지 않았다. 테스트 파일은 output/save-recovery 아래의 별도 경로에 생성했다.

다시 실행 (프로젝트 루트 PowerShell):
& ./output/save-recovery/CompileProject.ps1
& ./output/save-recovery/SetupAndRun.ps1

필요 환경: 설치된 Unity 2022.3.62f2, dotnet SDK 9.0.201, 프로젝트의 기존 Library DLL/PackageCache.
SetupAndRun은 tmp/save-recovery-regression 프로젝트를 만들고 숨긴 Unity 프로세스로 실행한다.
검증 코드의 의도적인 오류 시나리오는 Unity 로그에 오류를 남긴다. 테스트 통과 여부는 verification.txt와 프로세스 종료 코드로 확인한다.
