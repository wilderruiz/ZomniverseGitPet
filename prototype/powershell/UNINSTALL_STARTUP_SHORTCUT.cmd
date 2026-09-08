@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=[IO.Path]::Combine([Environment]::GetFolderPath('Startup'),'ZomniverseGitPet Prototype.lnk'); if(Test-Path $p){Remove-Item $p -Force}"
echo Startup shortcut removed.
pause
endlocal


