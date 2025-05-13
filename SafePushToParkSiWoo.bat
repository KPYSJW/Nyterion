@echo off
chcp 949 >nul
echo [Unity Git Push Automation - ParkSiWoo Branch Only]

:: Git ��ġ Ȯ��
git --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: Git is not installed. Please install Git.
    pause
    exit /b 1
)

:: Git ����� Ȯ��
if not exist ".git" (
    echo ERROR: This is not a Git repository. Initialization required.
    pause
    exit /b 1
)

:: .gitignore�� ��ũ��Ʈ ���� �߰�
set "SCRIPT_FILE=SafePushToParkSiWoo.bat"
if exist ".gitignore" (
    findstr /C:"%SCRIPT_FILE%" ".gitignore" >nul
    if %ERRORLEVEL% neq 0 (
        echo %SCRIPT_FILE% >> ".gitignore"
        echo .gitignore�� '%SCRIPT_FILE%' �߰� �Ϸ�
    ) else (
        echo .gitignore�� '%SCRIPT_FILE%' �̹� ����
    )
) else (
    echo %SCRIPT_FILE% > ".gitignore"
    echo .gitignore ���� ���� �� '%SCRIPT_FILE%' ��� �Ϸ�
)

:: ���� ����� ����
set "REMOTE_URL=https://github.com/KPYSJW/RoguelikePrototype.git"
git remote get-url origin >nul 2>&1
if %ERRORLEVEL% neq 0 (
    git remote add origin %REMOTE_URL%
    echo origin ���� ����� �߰���
) else (
    git remote set-url origin %REMOTE_URL%
    echo origin URL ������Ʈ �Ϸ�
)

:: ���� �귣ġ Ȯ�� �� �ڵ� ��ȯ
for /f "tokens=*" %%i in ('git branch --show-current') do set "CURRENT_BRANCH=%%i"
if not "%CURRENT_BRANCH%"=="ParkSiWoo" (
    echo ���� �귣ġ '%CURRENT_BRANCH%' �� ParkSiWoo�� �ڵ� ��ȯ�մϴ�.
    :: ���� ���� Ȯ�� �� �ؼ�
    git status >nul 2>&1
    for /f "tokens=*" %%s in ('git status ^| findstr "merging"') do (
        echo WARNING: ���� ���� ���°� ������. ������ �����մϴ�.
        git merge --quit
        if %ERRORLEVEL% neq 0 (
            echo ERROR: ���� ���� ����. �������� 'git merge --quit'�� �����ϼ���.
            pause
            exit /b 1
        )
    )
    git checkout ParkSiWoo 2>&1 | findstr "error" >nul
    if %ERRORLEVEL% equ 0 (
        echo ERROR: �귣ġ ��ȯ ����. ParkSiWoo �귣ġ�� �����մϴ�.
        git branch ParkSiWoo
        git checkout ParkSiWoo
    )
    echo �귣ġ�� ParkSiWoo�� ��ȯ�Ǿ����ϴ�.
)

:: ���� ���� Ȯ�� �� �����
echo DEBUG: Checking for changes...
git status --porcelain > temp_status.txt 2>&1
type temp_status.txt
if %ERRORLEVEL% equ 0 (
    :: Untracked ����(??) �Ǵ� ����� ���� ����
    findstr /R /C:"^[?][?]" /C:"^[MADRCU]" temp_status.txt >nul
    if %ERRORLEVEL% equ 0 (
        echo DEBUG: Changes or untracked files detected.
    ) else (
        echo ����� ������ �����ϴ�. �����մϴ�.
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

:: ������¡
echo Staging all changes...
git add .
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to stage changes.
    pause
    exit /b 1
)

:: ������¡ �� ���� ��Ȯ��
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

:: Ŀ�� �޽��� ���� (�μ��� �ްų� �⺻�� ���)
if "%1"=="" (
    set "COMMIT_MSG=Auto commit"
) else (
    set "COMMIT_MSG=%1"
)

:: Ŀ�� �� Ǫ��
git commit -m "%COMMIT_MSG%"
if %ERRORLEVEL% neq 0 (
    echo ERROR: Ŀ�� ����. ���� ������ Ȯ���ϼ���.
    pause
    exit /b 1
)

git push origin ParkSiWoo
if %ERRORLEVEL% neq 0 (
    echo ERROR: Push ����. ��Ʈ��ũ ���� �Ǵ� ������ Ȯ���ϼ���.
    pause
    exit /b 1
)

echo Push ����! ParkSiWoo �귣ġ�� �����ϰ� �ݿ��Ǿ����ϴ�.
pause