@echo off
setlocal enabledelayedexpansion

echo ========================================
echo       NuGet Cache Cleanup Tool
echo ========================================
echo.

set "NUGET_CACHE=%USERPROFILE%\.nuget\packages"
set "SCRIPT_DIR=%~dp0"
set "COUNT=0"

echo Scanning directory: %SCRIPT_DIR%
echo Cache directory: %NUGET_CACHE%
echo.
echo Starting cache cleanup...
echo ----------------------------------------

:: Loop through all .nupkg and .snupkg files in current directory
for %%f in ("%SCRIPT_DIR%*.nupkg" "%SCRIPT_DIR%*.snupkg") do (
    if exist "%%f" (
        set "filename=%%~nf"
        
        for /f "tokens=1,2,3,4,5 delims=." %%a in ("!filename!") do (
            set "part1=%%a"
            set "part2=%%b"
            set "part3=%%c"
            set "part4=%%d"
            set "part5=%%e"
        )
        
        call :ExtractPackageName "!filename!"
    )
)

echo ----------------------------------------
echo Cleaned !COUNT! package caches
echo ========================================
echo Done!
pause
exit /b

:ExtractPackageName
set "fullname=%~1"
set "pkgname="

for /f "tokens=1,* delims=." %%a in ("%fullname%") do (
    set "firstpart=%%a"
    set "rest=%%b"
)

set "pkgname=%firstpart%"
set "remaining=%rest%"

:BuildName
if "%remaining%"=="" goto :DoClean
for /f "tokens=1,* delims=." %%a in ("%remaining%") do (
    set "current=%%a"
    set "remaining=%%b"
    
    echo !current!| findstr /r "^[0-9]" >nul
    if errorlevel 1 (
        set "pkgname=!pkgname!.!current!"
        goto :BuildName
    )
)

:DoClean
if not "!pkgname!"=="" (
    set "cachepath=%NUGET_CACHE%\!pkgname!"
    if exist "!cachepath!" (
        echo Cleaning: !pkgname!
        rd /s /q "!cachepath!" 2>nul
        set /a COUNT+=1
    ) else (
        echo Skipped: !pkgname! ^(cache not found^)
    )
)
exit /b