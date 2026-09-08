@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "PS1=%ROOT%app\ZomniverseGitPetPrototype.ps1"

echo ============================================================
echo ZomniverseGitPet Prototype - Startup Diagnostic
 echo ============================================================
echo Script: %PS1%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%ROOT%' -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue"
echo.
echo [1/3] PowerShell version
powershell.exe -NoProfile -Command "$PSVersionTable.PSVersion.ToString()"
echo.
echo [2/3] Parsing app
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile('%PS1%',[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e | Format-List *; exit 2}else{'Parse OK'}"
if errorlevel 1 goto :end

echo.
echo [3/3] Launching app. Keep this window open while testing.
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%PS1%"
echo.
echo App exited with code %ERRORLEVEL%.

:end
echo.
pause


