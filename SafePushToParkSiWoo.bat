@echo off
chcp 949 >nul
echo [Unity Git Push Automation - ParkSiWoo Branch Only]

:: Git 설치 확인
git --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: Git is not installed. Please install Git.
    pause
    exit /b 1
)

:: Git 저장소 확인
if not exist ".git" (
    echo ERROR: This is not a Git repository. Initialization required.
    pause
    exit /b 1
)

:: .gitignore에 스크립트 제외 추가
set "SCRIPT_FILE=SafePushToParkSiWoo.bat"
if exist ".gitignore" (
    findstr /C:"%SCRIPT_FILE%" ".gitignore" >nul
    if %ERRORLEVEL% neq 0 (
        echo %SCRIPT_FILE% >> ".gitignore"
        echo .gitignore에 '%SCRIPT_FILE%' 추가 완료
    ) else (
        echo .gitignore에 '%SCRIPT_FILE%' 이미 존재
    )
) else (
    echo %SCRIPT_FILE% > ".gitignore"
    echo .gitignore 파일 생성 및 '%SCRIPT_FILE%' 등록 완료
)

:: 원격 저장소 설정
set "REMOTE_URL=https://github.com/KPYSJW/RoguelikePrototype.git"
git remote get-url origin >nul 2>&1
if %ERRORLEVEL% neq 0 (
    git remote add origin %REMOTE_URL%
    echo origin 원격 저장소 추가됨
) else (
    git remote set-url origin %REMOTE_URL%
    echo origin URL 업데이트 완료
)

:: 현재 브랜치 확인 및 자동 전환
for /f "tokens=*" %%i in ('git branch --show-current') do set "CURRENT_BRANCH=%%i"
if not "%CURRENT_BRANCH%"=="ParkSiWoo" (
    echo 현재 브랜치 '%CURRENT_BRANCH%' → ParkSiWoo로 자동 전환합니다.
    :: 병합 상태 확인 및 해소
    git status >nul 2>&1
    for /f "tokens=*" %%s in ('git status ^| findstr "merging"') do (
        echo WARNING: 병합 중인 상태가 감지됨. 병합을 종료합니다.
        git merge --quit
        if %ERRORLEVEL% neq 0 (
            echo ERROR: 병합 종료 실패. 수동으로 'git merge --quit'를 실행하세요.
            pause
            exit /b 1
        )
    )
    git checkout ParkSiWoo 2>&1 | findstr "error" >nul
    if %ERRORLEVEL% equ 0 (
        echo ERROR: 브랜치 전환 실패. ParkSiWoo 브랜치를 생성합니다.
        git branch ParkSiWoo
        git checkout ParkSiWoo
    )
    echo 브랜치가 ParkSiWoo로 전환되었습니다.
)

:: 변경 사항 확인 및 디버깅
echo DEBUG: Checking for changes...
git status --porcelain > temp_status.txt 2>&1
type temp_status.txt
if %ERRORLEVEL% equ 0 (
    :: Untracked 파일(??) 또는 변경된 파일 감지
    findstr /R /C:"^[?][?]" /C:"^[MADRCU]" temp_status.txt >nul
    if %ERRORLEVEL% equ 0 (
        echo DEBUG: Changes or untracked files detected.
    ) else (
        echo 변경된 파일이 없습니다. 종료합니다.
        del temp_status.txt
        pause
        exit /b 0
    )
) else (
    echo ERROR: Failed to check status.
    del temp_status.txt
    pause
    exit /b 1
)
del temp_status.txt

:: 스테이징
echo Staging all changes...
git add .
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to stage changes.
    pause
    exit /b 1
)

:: 스테이징 후 상태 재확인
echo DEBUG: After staging, checking status...
git status --porcelain > temp_status.txt 2>&1
type temp_status.txt
findstr /R /C:"^[MADRCU]" temp_status.txt >nul
if %ERRORLEVEL% neq 0 (
    echo ERROR: No changes to commit after staging.
    del temp_status.txt
    pause
    exit /b 1
)
del temp_status.txt

:: 커밋 메시지 설정 (인수로 받거나 기본값 사용)
if "%1"=="" (
    set "COMMIT_MSG=Auto commit"
) else (
    set "COMMIT_MSG=%1"
)

:: 커밋 및 푸시
git commit -m "%COMMIT_MSG%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: 커밋 실패. 변경 사항을 확인하세요.
    pause
    exit /b 1
)

git push origin ParkSiWoo
if %ERRORLEVEL% neq 0 (
    echo ERROR: Push 실패. 네트워크 상태 또는 권한을 확인하세요.
    pause
    exit /b 1
)

echo Push 성공! ParkSiWoo 브랜치에 안전하게 반영되었습니다.
pause