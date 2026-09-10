# On each update

1) Fully exit ZomniverseGitPet before updating.

2) From the repo folder:

```powershell
cd "I:\Dropbox\WORK_LAPTOP\Programming\ZomniverseGitPet"
````

3. Publish the latest local code:

publish-local.ps1 is your fast development loop. It rebuilds the current app as a self-contained single EXE and replaces the copy you run from:
I:\Dropbox\WORK_LAPTOP\Programming\ZomniverseGitPet_Releases\current\ZomniverseGitPet.exe
That is why it has been perfect for what we've been doing: change code → close GitPet → run publish-local.ps1 → reopen and inspect the change.'

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-local.ps1
```

build-release.ps1 is the distribution/package factory. It is much heavier. It builds the solution, runs the regression suite, performs the self-contained publish, creates a portable versioned EXE, calculates release hashes/metadata, and then invokes Inno Setup to manufacture the actual installer for other users.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-release.ps1
```

So your workflow should be:

NORMAL DEVELOPMENT
edit code
   ↓
publish-local.ps1
   ↓
test GitPet
   ↓
repeat

Only when we say, essentially, “this version is ready to package”, use:

RELEASE PREPARATION
tests/build verified
   ↓
build-release.ps1
   ↓
portable EXE
+ installer EXE
+ manifest
+ SHA-256 hashes
   ↓
GitHub Release


So keep using this after ordinary patches:

powershell -ExecutionPolicy Bypass -File scripts\publish-local.ps1
You do not need to switch your everyday routine to build-release.ps1.

The new one is for occasions like:

“We finished 0.4.1; now make the package another person can actually install.”

Your existing publish-local.ps1 deliberately publishes directly into the development current location, whereas the new release builder creates versioned packages instead.

And once the installer/update system is finished, this separation becomes especially useful: your development builds stay your development builds; public releases are deliberate, versioned artifacts.



4. Launch:

```text
I:\Dropbox\WORK_LAPTOP\Programming\ZomniverseGitPet_Releases\current\ZomniverseGitPet.exe
```

5. Reproduce the exact screen/action changed and verify it visually.
