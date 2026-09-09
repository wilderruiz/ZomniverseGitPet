<p align="center">
  <img src="mockups/pet/svg/pet_happy_01.svg" width="180" alt="ZomniverseGitPet purple fox guardian">
</p>

<h1 align="center">ZomniverseGitPet</h1>

<p align="center"><strong>A desktop Git guardian for human and AI-assisted development.</strong></p>

<p align="center">
  <strong>Version 0.4.1</strong>
  &nbsp;•&nbsp;
  Windows 10 / 11
  &nbsp;•&nbsp;
  .NET 8
  &nbsp;•&nbsp;
  MIT License
</p>

<p align="center">
  <strong>Review what changed → Save locally → Get remote updates → Send saved updates</strong>
</p>

ZomniverseGitPet is a lightweight Windows desktop companion that turns Git safety into a visible, low-friction workflow. A small purple fox stays near the workspace while the Guardian Console explains repository state in plain English, reviews changed files, keeps local save points, runs project tests, protects milestones, and performs remote Git actions only when the user explicitly requests them.

It is designed for developers, people working with AI coding agents, and anyone who wants Git protection without having to translate every normal action into terminal vocabulary first.

> **Current release:** `0.4.1` · native .NET 8 WinForms · Windows x64 · Git for Windows

---

<h2>Current experience</h2>

<table>
  <tr>
    <td width="25%"><strong>Review</strong><br><sub>Inspect unsaved files and compare the latest saved version with the current version.</sub></td>
    <td width="25%"><strong>Save</strong><br><sub>Create an ordinary local Git commit after preview and confirmation.</sub></td>
    <td width="25%"><strong>Get ↓</strong><br><sub>Bring remote commits into the current branch with fast-forward-only safety.</sub></td>
    <td width="25%"><strong>Send ↑</strong><br><sub>Send saved local commits to the configured origin. Unsaved work is never included.</sub></td>
  </tr>
</table>

The Guardian header distinguishes three things that Git users often have to infer manually:

<table>
  <tr><th>What GitPet shows</th><th>Meaning</th></tr>
  <tr><td><strong>Unsaved changes</strong></td><td>Files changed on this PC since the latest local save.</td></tr>
  <tr><td><strong>Saved updates ready to send</strong></td><td>Local commits ahead of the tracked remote branch.</td></tr>
  <tr><td><strong>Updates ready to get</strong></td><td>Remote commits not yet present locally.</td></tr>
</table>

The toolbar intentionally uses human-first labels: <strong>Projects · Refresh · Review · Tests · Save · Get ↓ · Send ↑ · History · Health</strong>.

---

<h2>File Review</h2>

Click any changed file and the lower Guardian area becomes a resizable comparison workspace.

<table>
  <tr>
    <th width="50%">SAVED VERSION</th>
    <th width="50%">CURRENT VERSION</th>
  </tr>
  <tr>
    <td>Latest local commit / save point</td>
    <td>Current file on disk</td>
  </tr>
  <tr>
    <td>Human summary by default</td>
    <td>Human summary by default</td>
  </tr>
  <tr>
    <td>Technical source view when the item is text</td>
    <td>Technical source view when the item is text</td>
  </tr>
  <tr>
    <td>Changed baseline lines highlighted</td>
    <td>Changed current lines highlighted</td>
  </tr>
</table>

The default **Human view** answers the useful question first: what happened to this file? The optional **Technical view** shows the actual source text for ordinary text files. GitPet keeps the project's own indentation and content; the viewer is read-only.

Untracked folders are expanded to individual untracked files, so a new file such as `examples/showcase.html` can be selected directly and its HTML/CSS source can be reviewed instead of treating the whole folder as one opaque change.

New files clearly show that no saved version existed. Deleted files show that the current version is gone. Binary or very large files remain protected from accidental source rendering and can be opened or located instead.

---

<h2>Meet the guardian</h2>

<table>
  <tr>
    <th>Quiet sentinel</th>
    <th>All checks green</th>
    <th>Changes spotted</th>
    <th>Watchful caution</th>
  </tr>
  <tr>
    <td align="center"><img src="mockups/pet/svg/pet_idle_01.svg" width="120" alt="Idle fox"></td>
    <td align="center"><img src="mockups/pet/svg/pet_happy_01.svg" width="120" alt="Happy fox"></td>
    <td align="center"><img src="mockups/pet/svg/pet_review_ready_01.svg" width="120" alt="Review-ready fox"></td>
    <td align="center"><img src="mockups/pet/svg/pet_warning_01.svg" width="120" alt="Warning fox"></td>
  </tr>
  <tr>
    <td align="center">Waiting / checking</td>
    <td align="center">Repository healthy</td>
    <td align="center">Changes ready to review</td>
    <td align="center">Git needs attention</td>
  </tr>
</table>

The executable, taskbar, Guardian window, and tray all use the same canonical fox-head identity. Additional approved mascot states remain available for richer idle/sleep behavior later.

---

<h2>How the application is structured</h2>

<table>
  <tr><th colspan="3">ZomniverseGitPet 0.4.1 architecture</th></tr>
  <tr>
    <td align="center"><strong>Desktop shell</strong><br><sub>PetForm · tray · single-instance activation</sub></td>
    <td align="center">→</td>
    <td align="center"><strong>Guardian Console</strong><br><sub>GuardianForm · File Review · project setup · milestones</sub></td>
  </tr>
  <tr>
    <td align="center"><strong>Typed local state</strong><br><sub>AppConfig · recent projects · per-project tests · UI state</sub></td>
    <td align="center">↔</td>
    <td align="center"><strong>Git service layer</strong><br><sub>status · save · get · send · history · health · identity · remotes</sub></td>
  </tr>
  <tr>
    <td align="center"><strong>Safety helpers</strong><br><sub>scope planner · ignore advisor · safe-directory flow · milestone coordinator</sub></td>
    <td align="center">↔</td>
    <td align="center"><strong>Local repository</strong><br><sub>ordinary Git metadata and working files remain authoritative</sub></td>
  </tr>
  <tr>
    <td align="center"><strong>Tests</strong><br><sub>lightweight regression suite + project-specific commands</sub></td>
    <td align="center">→</td>
    <td align="center"><strong>Audit trail</strong><br><sub>append-only local activity metadata under LocalAppData</sub></td>
  </tr>
</table>

<details>
<summary><strong>Important source areas</strong></summary>

| Area | Responsibility |
| --- | --- |
| `src/ZomniverseGitPet/GuardianForm.cs` | Main Guardian UI, project state, Save/Get/Send interactions |
| `src/ZomniverseGitPet/FileComparisonPanel.cs` | Human and Technical File Review |
| `src/ZomniverseGitPet/GitService.cs` | Serialized Git process execution and repository operations |
| `src/ZomniverseGitPet/FriendlyGitState.cs` | Human-readable sync state and Send readiness |
| `src/ZomniverseGitPet/AppConfig.cs` | Typed per-user configuration and recent-project registry |
| `src/ZomniverseGitPet/ProjectScopePlanner.cs` | Selective project tracking scope |
| `src/ZomniverseGitPet/GitIgnoreAdvisor.cs` | Safe `.gitignore` guidance and application |
| `src/ZomniverseGitPet/MajorUpdateCoordinator.cs` | Legacy branch/tag and generation planning |
| `tests/ZomniverseGitPet.Tests/` | Lightweight regression checks |
| `scripts/publish-local.ps1` | Local self-contained Windows build workflow |

</details>

---

<h2>Project workflow</h2>

<table>
  <tr><td align="center"><strong>1 · Choose or open a folder</strong></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>2 · GitPet inspects it</strong></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>Existing Git project?</strong> Open safely &nbsp; · &nbsp; <strong>Ordinary folder?</strong> Offer Git preparation</td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>3 · Choose what belongs to the project</strong><br><sub>Selective scope and nested-repository protection</sub></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>4 · Review repository hygiene</strong><br><sub>Detected ignore candidates · presets · custom rules · CURRENT / AFTER preview</sub></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>5 · Monitor and Review changed files</strong><br><sub>Human summary or Technical source comparison</sub></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>6 · Run Tests when useful</strong></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>7 · Save locally</strong><br><sub>Ordinary local Git commit after preview and confirmation</sub></td></tr>
  <tr><td align="center">↓</td></tr>
  <tr><td align="center"><strong>8 · Get ↓ / Send ↑</strong><br><sub>Explicit remote actions only</sub></td></tr>
</table>

When a selected folder is not already a Git repository, GitPet can prepare it only after the user approves the scope and repository-hygiene plan. The Git initialization command it ultimately runs is a real command and is therefore shown as code:

```bash
git init -b main
```

Project preparation does **not** create an online hosting repository, configure `origin`, stage files, create the first save, get remote changes, or send anything. Those remain separate actions.

---

<h2>Friendly repository hygiene</h2>

GitPet separates ignore decisions into two layers:

<table>
  <tr><th>Layer</th><th>Purpose</th></tr>
  <tr><td><strong>Detected in this project</strong></td><td>Project-specific privacy/generated candidates discovered inside the selected scope.</td></tr>
  <tr><td><strong>Ignore library + your own rules</strong></td><td>Reusable privacy, generated, system, archive, and custom choices.</td></tr>
</table>

The environment preset is documentation rather than a command, so it is presented as a readable table instead of a copy/paste code block:

<table>
  <tr><th>Pattern</th><th>Effect</th></tr>
  <tr><td><code>.env</code></td><td>Ignore the base environment file.</td></tr>
  <tr><td><code>.env.*</code></td><td>Ignore environment variants.</td></tr>
  <tr><td><code>!.env.example</code></td><td>Keep the simple example template.</td></tr>
  <tr><td><code>!.env.*.example</code></td><td>Keep named example templates.</td></tr>
  <tr><td><code>!.env.sample</code></td><td>Keep the simple sample template.</td></tr>
  <tr><td><code>!.env.*.sample</code></td><td>Keep named sample templates.</td></tr>
  <tr><td><code>!.env.template</code></td><td>Keep the simple template file.</td></tr>
  <tr><td><code>!.env.*.template</code></td><td>Keep named template files.</td></tr>
  <tr><td><code>!.env.dist</code></td><td>Keep the simple distribution template.</td></tr>
  <tr><td><code>!.env.*.dist</code></td><td>Keep named distribution templates.</td></tr>
</table>

The custom builder translates friendly choices automatically:

| User chooses | Example input | Generated Git pattern | Meaning |
| --- | --- | --- | --- |
| Folder name | `cache` | `cache/` | ignore folders named cache throughout the project |
| File extension | `tmp` | `*.tmp` | ignore `.tmp` files throughout the project |
| File name | `secrets.json` | `secrets.json` | ignore that file name throughout the project |
| Name contains | `LEGACY` | `**/*LEGACY*` | ignore files/folders containing that text anywhere |

The right side of the setup review contains draggable **CURRENT** and **AFTER** views. Existing root content is preserved; nested `.gitignore` files are shown for context but are not silently rewritten.

---

<h2>Tests</h2>

Each remembered project can keep its own local test profile. GitPet can suggest likely commands from common project metadata, but discovery is advisory and does not edit project files.

Examples include `npm test`, `dotnet test`, `python -m pytest`, `composer test`, and `cargo test` when corresponding project metadata is detected.

Manual test runs execute saved commands in order, stop on the first failure, and show PASS/FAIL output in **Guardian Activity**. Hold <kbd>Shift</kbd> while clicking **Tests** to edit an existing project's saved commands.

---

<h2>Save, Get and Send</h2>

<h3>Save</h3>

Save previews every current non-ignored change, asks for confirmation, verifies that Git has an author identity, stages the approved working state, and creates an ordinary **local** Git commit. Saving never sends anything automatically.

<h3>Get ↓</h3>

Get is manual and requires a clean working tree. After confirmation GitPet runs the equivalent of:

```bash
git pull --ff-only origin <current-branch>
```

Fast-forward-only means GitPet will not create an automatic merge commit. If local and remote history diverge, Get stops safely.

<h3>Send ↑</h3>

Send works only with already-saved commits. Before sending, GitPet distinguishes these cases:

<table>
  <tr><th>State</th><th>GitPet response</th></tr>
  <tr><td>Unsaved work, nothing saved ahead</td><td>Ask the user to Save first.</td></tr>
  <tr><td>No unsaved work, nothing saved ahead</td><td>Explain that the remote is already up to date.</td></tr>
  <tr><td>Saved commits ready, no unsaved work</td><td>Offer normal Send confirmation.</td></tr>
  <tr><td>Saved commits ready plus newer unsaved work</td><td>Allow sending only the already-saved commits and clearly exclude unsaved work.</td></tr>
</table>

The actual Git command is:

```bash
git push origin <current-branch>
```

GitPet never force-pushes `main`, never stages or commits as a side effect of Send, and never invents an online repository.

---

<h2>Connecting an existing remote</h2>

A local repository and an online repository are separate things. If Get or Send needs `origin` and no readable remote exists, GitPet opens **Connect Remote** and asks the user for the clone address of an existing repository.

Example addresses are documentation, not terminal commands: <code>https://github.com/user/project.git</code> or <code>git@github.com:user/project.git</code>.

After explicit approval, GitPet performs the real Git command:

```bash
git remote add origin <user-provided-url>
```

It does not replace an existing `origin`, create the online repository, Save, Get, or Send merely because the remote was connected.

---

<h2>Major updates and milestones</h2>

GitPet can flag unusually large or structural changes and ask whether the project may be entering a new generation. The user decides; detection is advisory.

Signals can include high file/line churn, many additions/removals together, schema or dependency-structure changes, format replacements, or local/remote divergence.

The **Milestones / Major update? ✦** flow can preserve a previous generation as an ordinary legacy branch and annotated tag while continuing redesign work on a separate branch. It does not rewrite `main`, reset working files, clean the repository, or publish a release automatically.

For GitHub remotes it can also open GitHub's release editor for a protected legacy tag and navigate to previous releases.

See [Major updates, milestones, and releases](docs/MAJOR_UPDATES_AND_RELEASES.md) for the detailed model.

---

<h2>Safety contract</h2>

<table>
  <tr><th>GitPet may do</th><th>GitPet does not do automatically</th></tr>
  <tr><td>Read repository status and history</td><td>Pull/Get remote changes</td></tr>
  <tr><td>Review saved/current files</td><td>Push/Send commits</td></tr>
  <tr><td>Create confirmed local saves</td><td>Create online repositories</td></tr>
  <tr><td>Run configured project tests</td><td>Replace an existing remote</td></tr>
  <tr><td>Add an explicitly approved origin</td><td>Force-push main</td></tr>
  <tr><td>Use exact-path safe.directory approval</td><td>Use <code>safe.directory=*</code></td></tr>
  <tr><td>Create explicit milestone branches/tags</td><td>Use destructive <code>reset --hard</code> or <code>clean</code> workflows inside the app</td></tr>
</table>

Background repository checks run quietly and do not masquerade as foreground work. Automatic Saving is **off by default**, creates local saves only, and never triggers Get or Send.

---

<h2>Configuration</h2>

ZomniverseGitPet stores typed JSON configuration and an append-only audit trail beneath `%LOCALAPPDATA%\ZomniverseGitPet`.

Configuration can include the active repository, up to 20 recent repositories, project-specific test commands, automatic-save preferences, and local UI convenience state. Machine-specific paths are not built into the public source tree.

---

<h2>Build</h2>

Requirements: Windows, the .NET 8 SDK, and Git for Windows available as `git.exe`.

```powershell
dotnet build ZomniverseGitPet.sln -c Release
dotnet run --project tests/ZomniverseGitPet.Tests/ZomniverseGitPet.Tests.csproj -c Release
```

Create a self-contained single executable:

```powershell
dotnet publish src/ZomniverseGitPet/ZomniverseGitPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

For local development, the repository includes a convenience publisher:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-local.ps1
```

That script places the runnable development executable in the sibling `ZomniverseGitPet_Releases/current` directory while ordinary compiler output remains under ignored `bin/` and `obj/` directories.

---

<h2>Project status</h2>

<strong>ZomniverseGitPet 0.4.1</strong> is an early public preview. The maintained application is the native C#/.NET 8 WinForms project under `src/ZomniverseGitPet/`. The original PowerShell proof of concept remains under `prototype/powershell/` as a historical reference.

Current platform support: Windows 10/11 x64 with Git for Windows.

<details>
<summary><strong>Near-term roadmap</strong></summary>

- Micro-animation and richer repository-state transitions
- Richer shared repository snapshot/state model across pet and Guardian
- Curated screenshot/feature gallery from a safe demo repository
- In-app settings editor for automatic-saving policy
- File-system-assisted refresh with polling fallback
- Signed release artifacts and installer packaging
- Broader accessibility and multi-monitor refinements
- Task/worktree-aware saves for parallel AI agents
- Additional automated integration and UI tests

</details>

---

<h2>License</h2>

ZomniverseGitPet is available under the [MIT License](LICENSE).
