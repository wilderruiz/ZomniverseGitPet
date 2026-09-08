@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Startup'),'ZomniverseGitPet Prototype.lnk')); $s.TargetPath=[IO.Path]::Combine('%~dp0','ZomniverseGitPetPrototype.vbs'); $s.WorkingDirectory='%~dp0'; $s.Save()"
echo ZomniverseGitPet Prototype will now start when you sign in to Windows.
pause
endlocal


