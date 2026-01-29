@echo off
setlocal enabledelayedexpansion
title NuGet Push Tool

cd /d "%~dp0"

echo ============================================
echo           NuGet Push Tool
echo ============================================
echo.

set "fileCount=0"
for /f "delims=" %%a in ('dir /b/a-d/oN *.nupkg 2^>nul') do set /a fileCount+=1
if %fileCount%==0 (
    echo [Error] No .nupkg files found in current directory!
    goto :end
)
echo Found %fileCount% nupkg files to push
echo.

:selectSource
echo Please select push target:
echo   1. ms     - nuget.org (Official)
echo   2. github - GitHub Packages
echo.
set /p "source=Enter option (ms/github): "

if /i "%source%"=="" (
    echo [Error] Input cannot be empty!
    goto :selectSource
)
if /i "%source%"=="ms" (
    set "source_url=https://api.nuget.org/v3/index.json"
    set "source_name=nuget.org"
) else if /i "%source%"=="github" (
    set "source_url=https://nuget.pkg.github.com/OWNER/index.json"
    set "source_name=GitHub Packages"
) else (
    echo [Error] Invalid option, please enter ms or github!
    goto :selectSource
)

:inputKey
echo.
set /p "api_key=Enter your API Key: "
if "%api_key%"=="" (
    echo [Error] API Key cannot be empty!
    goto :inputKey
)

echo.
echo ============================================
echo Target: %source_name%
echo Source: %source_url%
echo Files:  %fileCount%
echo ============================================
echo.
set /p "confirm=Confirm push? (Y/N): "
if /i not "%confirm%"=="Y" (
    echo Push cancelled
    goto :end
)

echo.
echo [Starting push...]
echo.

set "successCount=0"
set "failCount=0"

for /f "delims=" %%a in ('dir /b/a-d/oN *.nupkg') do (
    echo --------------------------------------------
    echo [Pushing] %%a
    dotnet nuget push "%%a" --api-key "%api_key%" --source "%source_url%" --skip-duplicate
    if !errorlevel!==0 (
        echo [Success] %%a
        set /a successCount+=1
    ) else (
        echo [Failed] %%a
        set /a failCount+=1
    )
)

echo.
echo ============================================
echo              Push Complete
echo ============================================
echo Success: %successCount%
echo Failed:  %failCount%
echo Total:   %fileCount%
echo ============================================

:end
echo.
pause
endlocal