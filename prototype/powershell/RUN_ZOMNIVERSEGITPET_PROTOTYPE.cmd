@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "PS1=%ROOT%app\ZomniverseGitPetPrototype.ps1"
set "LOG=%LOCALAPPDATA%\ZomniverseGitPetPrototype\startup-error.log"

if not exist "%LOCALAPPDATA%\ZomniverseGitPetPrototype" mkdir "%LOCALAPPDATA%\ZomniverseGitPetPrototype" >nul 2>&1

rem Remove Mark-of-the-Web only from this extracted Git Pet package.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%ROOT%' -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue" >nul 2>&1

rem Parse-check the app before starting it.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile('%PS1%',[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e | ForEach-Object { $_.ToString() }; exit 2}" > "%LOG%" 2>&1
if errorlevel 1 goto :failed

rem Launch hidden and return immediately. Use RUN_DIAGNOSTIC.cmd if troubleshooting is needed.
start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -STA -File "%PS1%"
exit /b 0

:failed
echo ZomniverseGitPet Prototype could not start.
echo.
echo The diagnostic log will open now:
echo %LOG%
echo.
start "" notepad.exe "%LOG%"
pause
exit /b 1


