# On each update

1) Fully exit ZomniverseGitPet before updating.

2) From the repo folder:

```powershell
cd "I:\Dropbox\WORK_LAPTOP\Programming\ZomniverseGitPet"
````

3. Publish the latest local code:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-local.ps1
```

This recompiles the app and replaces the current EXE.

If it says the EXE is locked, close GitPet completely and run the command again.

4. Launch:

```text
I:\Dropbox\WORK_LAPTOP\Programming\ZomniverseGitPet_Releases\current\ZomniverseGitPet.exe
```

5. Reproduce the exact screen/action changed and verify it visually.
