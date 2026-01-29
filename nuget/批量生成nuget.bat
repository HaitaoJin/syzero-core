@echo off
chcp 936 >nul
setlocal enabledelayedexpansion

set "SolutionPath=..\src\SyZero.sln"
set "Configuration=Release"

cd /d "%~dp0"

echo ============================================
echo           SyZero NuGet Pack Tool
echo ============================================
echo.

if not exist "%SolutionPath%" (
    echo [Error] Solution file not found: %SolutionPath%
    goto :error
)

echo [1/4] Cleaning old NuGet packages...
del /q "*.nupkg" 2>nul
del /q "*.snupkg" 2>nul
echo      Done
echo.

echo [2/4] Restoring NuGet dependencies...
dotnet restore "%SolutionPath%" --verbosity minimal
if %errorlevel% neq 0 (
    echo [Error] Restore failed
    goto :error
)
echo      Done
echo.

echo [3/4] Building solution (%Configuration%)...
dotnet build "%SolutionPath%" --configuration %Configuration% --no-restore
if %errorlevel% neq 0 (
    echo [Error] Build failed
    goto :error
)
echo      Done
echo.

echo [4/4] Generating NuGet packages...
dotnet pack "%SolutionPath%" --configuration %Configuration% --no-build"
if %errorlevel% neq 0 (
    echo [Error] Pack failed
    goto :error
)
echo      Done
echo.

echo ============================================
echo Generated NuGet packages:
echo ============================================
set count=0
for %%f in (*.nupkg) do (
    echo   %%f
    set /a count+=1
)
echo.
echo Total: !count! packages
echo ============================================
echo [Success] All operations completed!
echo ============================================
goto :end

:error
echo.
echo ============================================
echo [Failed] Error occurred, please check above
echo ============================================

:end
echo.
endlocal
pause