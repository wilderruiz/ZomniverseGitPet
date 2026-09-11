using ZomniverseGitPet;

var failures = new List<string>();

Check("clean status", () =>
{
    var status = GitService.ParsePorcelainV2("# branch.head main\n");
    return status.Healthy && status.Branch == "main" && status.Files.Count == 0;
});

Check("changed file parsing", () =>
{
    var status = GitService.ParsePorcelainV2("# branch.head feature\n1 .M N... 100644 100644 100644 abc abc src/file.cs\n? notes/\n");
    return status.Files.Count == 2 && status.Files[0].Path == "src/file.cs" && status.Files[1].Status == "??";
});

Check("clean status without branch.ab keeps remote counts unknown", () =>
{
    var status = GitService.ParsePorcelainV2("# branch.head main\n");
    return !status.HasTrackingInformation && status.Ahead == 0 && status.Behind == 0;
});

Check("branch.ab parses ahead and behind", () =>
{
    var status = GitService.ParsePorcelainV2(
        "# branch.head main\n# branch.upstream origin/main\n# branch.ab +2 -1\n");
    return status.HasTrackingInformation && status.Ahead == 2 && status.Behind == 1;
});

Check("friendly sync state uses singular and plural grammar", () =>
{
    var singular = new RepositoryStatus(
        true, "main", [new ChangedFile(".M", "one.cs")],
        Ahead: 1, Behind: 1, HasTrackingInformation: true);
    var plural = new RepositoryStatus(
        true, "main", [new ChangedFile(".M", "one.cs"), new ChangedFile("??", "two.cs")],
        Ahead: 3, Behind: 2, HasTrackingInformation: true);

    return FriendlyGitState.FormatSyncSummary(singular) ==
               "1 unsaved change · 1 saved update ready to send · 1 update ready to get" &&
           FriendlyGitState.FormatSyncSummary(plural) ==
               "2 unsaved changes · 3 saved updates ready to send · 2 updates ready to get";
});

Check("unsaved plus ahead zero requires Save first", () =>
{
    var status = new RepositoryStatus(
        true, "main", [new ChangedFile(".M", "one.cs")],
        Ahead: 0, Behind: 0, HasTrackingInformation: true);
    return FriendlyGitState.GetSendReadiness(status) == SendReadiness.SaveFirst;
});

Check("clean plus ahead zero is already up to date", () =>
{
    var status = new RepositoryStatus(
        true, "main", [], Ahead: 0, Behind: 0, HasTrackingInformation: true);
    return FriendlyGitState.GetSendReadiness(status) == SendReadiness.AlreadyUpToDate;
});

Check("unsaved plus ahead allows sending saved commits", () =>
{
    var status = new RepositoryStatus(
        true, "main", [new ChangedFile(".M", "one.cs"), new ChangedFile("??", "two.cs")],
        Ahead: 2, Behind: 0, HasTrackingInformation: true);
    return FriendlyGitState.GetSendReadiness(status) == SendReadiness.Ready;
});

Check("suspicious path detection", () =>
{
    var hits = GitService.FindSuspiciousPaths([new ChangedFile("??", ".env"), new ChangedFile("M", "src/app.cs")], [@"(^|/)\.env($|\.)"]);
    return hits.Count == 1 && hits[0] == ".env";
});

Check("embedded pet asset pack", () =>
{
    var resources = typeof(GitService).Assembly.GetManifestResourceNames();
    var expected = new[]
    {
        "pet_idle_01.png", "pet_idle_02.png", "pet_happy_01.png",
        "pet_review_ready_01.png", "pet_warning_01.png", "pet_sleep_01.png"
    };
    return expected.All(file => resources.Any(resource => resource.EndsWith(file, StringComparison.Ordinal)));
});

Check("recent repository registry keeps newest 20", () =>
{
    var config = new AppConfig();
    for (var i = 0; i < 23; i++)
    {
        config.RememberRepository(Path.Combine(Path.GetTempPath(), "zgitpet-registry-" + i),
            DateTimeOffset.UtcNow.AddMinutes(i));
    }
    return config.RecentRepositories.Count == 20 &&
           config.RecentRepositories[0].DisplayName == "zgitpet-registry-22" &&
           config.RepositoryPath!.EndsWith("zgitpet-registry-22", StringComparison.OrdinalIgnoreCase);
});

Check("test commands stay isolated per project", () =>
{
    var rootA = Path.Combine(Path.GetTempPath(), "zgitpet-tests-a");
    var rootB = Path.Combine(Path.GetTempPath(), "zgitpet-tests-b");
    var config = new AppConfig();
    config.RememberRepository(rootA);
    config.SetTestCommandsForRepository(rootA, ["npm test", "npm run test:unit", "npm test"]);
    config.RememberRepository(rootB);
    config.SetTestCommandsForRepository(rootB, ["python -m pytest"]);
    config.RememberRepository(rootA);

    var a = config.GetTestCommandsForRepository(rootA);
    var b = config.GetTestCommandsForRepository(rootB);
    return a.SequenceEqual(["npm test", "npm run test:unit"]) &&
           b.SequenceEqual(["python -m pytest"]) &&
           config.RepositoryPath!.EndsWith("zgitpet-tests-a", StringComparison.OrdinalIgnoreCase);
});

Check("project test advisor suggests without changing project files", () =>
{
    var root = CreateTempDirectory();
    try
    {
        var package = Path.Combine(root, "package.json");
        var original = "{\"scripts\":{\"test\":\"node --test\",\"test:unit\":\"node --test tests/unit\",\"build\":\"vite build\"}}";
        File.WriteAllText(package, original);
        var suggestions = ProjectTestAdvisor.Suggest(root);
        var after = File.ReadAllText(package);
        return original == after &&
               suggestions.Contains("npm test") &&
               suggestions.Contains("npm run test:unit") &&
               suggestions.All(command => !command.Contains("build", StringComparison.OrdinalIgnoreCase));
    }
    finally { TryDelete(root); }
});

Check("zero-context diff maps before and now lines", () =>
{
    var diff = "@@ -10,2 +10,3 @@\n-old a\n-old b\n+new a\n+new b\n+new c\n";
    var map = DiffLineMap.ParseUnifiedZeroContext(diff);
    return map.BeforeLines.SetEquals([10, 11]) && map.AfterLines.SetEquals([10, 11, 12]);
});

Check("new-file diff maps only now lines", () =>
{
    var diff = "@@ -0,0 +1,4 @@\n+one\n+two\n+three\n+four\n";
    var map = DiffLineMap.ParseUnifiedZeroContext(diff);
    return map.BeforeLines.Count == 0 && map.AfterLines.SetEquals([1, 2, 3, 4]);
});

Check("safe splitter handles transient tiny bounds", () =>
{
    return SafeSplitContainer.CalculateSafeDistance(0, 6, 0.5, 120) == 0 &&
           SafeSplitContainer.CalculateSafeDistance(6, 6, 0.5, 120) == 0 &&
           SafeSplitContainer.CalculateSafeDistance(100, 6, 0.5, 120) == 47;
});

Check("safe splitter preserves useful ratio when space returns", () =>
{
    var distance = SafeSplitContainer.CalculateSafeDistance(1000, 6, 0.65, 120);
    return distance == 646 && distance >= 120 && distance <= 874;
});

Check("selective scope keeps deep siblings excluded", () =>
{
    var root = CreateTempDirectory();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "CV", "expertise"));
        Directory.CreateDirectory(Path.Combine(root, "CV", "applications"));
        File.WriteAllText(Path.Combine(root, "config.home.php"), "<?php");
        var plan = ProjectScopePlanner.Create(root,
            [new ProjectScopeEntry("CV/expertise", true), new ProjectScopeEntry("config.home.php", false)],
            trackEverything: false);
        var rules = ProjectScopePlanner.BuildIgnoreRules(plan);
        return rules.Contains("/*") &&
               rules.Contains("!/CV/") &&
               rules.Contains("/CV/*") &&
               rules.Contains("!/CV/expertise/") &&
               rules.Contains("!/CV/expertise/**") &&
               rules.Contains("!/config.home.php") &&
               !rules.Contains("!/CV/**");
    }
    finally { TryDelete(root); }
});

Check("selective scope excludes nested repositories", () =>
{
    var root = CreateTempDirectory();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "home", "app"));
        Directory.CreateDirectory(Path.Combine(root, "home", "app", ".git"));
        var plan = ProjectScopePlanner.Create(root, [new ProjectScopeEntry("home", true)], trackEverything: false);
        var rules = ProjectScopePlanner.BuildIgnoreRules(plan);
        return plan.NestedRepositories.Contains("home/app") && rules.Contains("/home/app/");
    }
    finally { TryDelete(root); }
});

Check("scoped hygiene sees nested gitignore and ignores env templates", () =>
{
    var root = CreateTempDirectory();
    try
    {
        var home = Path.Combine(root, "home");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(Path.Combine(home, "node_modules"));
        File.WriteAllText(Path.Combine(home, ".gitignore"), "node_modules/\r\n");
        File.WriteAllText(Path.Combine(home, ".env.development.example"), "EXAMPLE=1");
        var plan = ProjectScopePlanner.Create(root, [new ProjectScopeEntry("home", true)], trackEverything: false);
        var suggestions = ScopedGitIgnoreAdvisor.Suggest(plan);
        var documents = ScopedGitIgnoreAdvisor.FindIgnoreDocuments(plan);
        return suggestions.All(item => !item.Rule.Equals("node_modules/", StringComparison.OrdinalIgnoreCase)) &&
               suggestions.All(item => !item.Rule.Equals(".env.development.example", StringComparison.OrdinalIgnoreCase)) &&
               documents.Any(item => item.RelativePath.Equals("home/.gitignore", StringComparison.OrdinalIgnoreCase));
    }
    finally { TryDelete(root); }
});

Check("gitignore advisor finds common and security candidates safely", () =>
{
    var root = CreateTempDirectory();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        File.WriteAllText(Path.Combine(root, ".env"), "SECRET=test");
        File.WriteAllText(Path.Combine(root, ".env.local"), "SECRET=local");
        File.WriteAllText(Path.Combine(root, ".env.example"), "SECRET=example");
        var suggestions = GitIgnoreAdvisor.Suggest(root);
        return suggestions.Any(item => item.Rule == "bin/" && item.DefaultSelected) &&
               suggestions.Any(item => item.Rule == "node_modules/" && item.DefaultSelected) &&
               suggestions.Any(item => item.Rule == ".env" && item.Confidence == GitIgnoreConfidence.Security) &&
               suggestions.Any(item => item.Rule == ".env.local" && item.Confidence == GitIgnoreConfidence.Security) &&
               suggestions.All(item => item.Rule != ".env.example" && item.Rule != ".env.*");
    }
    finally { TryDelete(root); }
});

Check("gitignore append preserves existing content and avoids duplicates", () =>
{
    var root = CreateTempDirectory();
    try
    {
        var path = Path.Combine(root, ".gitignore");
        File.WriteAllText(path, "existing/\r\n");
        var added = GitIgnoreAdvisor.AppendAcceptedRules(root, ["bin/", ".env", "bin/"]);
        var text = File.ReadAllText(path);
        return added == 2 && text.Contains("existing/") && text.Contains("bin/") && text.Contains(".env") &&
               text.IndexOf("bin/", StringComparison.Ordinal) == text.LastIndexOf("bin/", StringComparison.Ordinal);
    }
    finally { TryDelete(root); }
});

Check("gitignore preview is exact and does not modify the file", () =>
{
    var root = CreateTempDirectory();
    try
    {
        var path = Path.Combine(root, ".gitignore");
        File.WriteAllText(path, "existing/\r\n");
        var before = File.ReadAllText(path);
        var preview = GitIgnoreAdvisor.BuildPreviewContent(root, ["bin/", ".env"]);
        var stillBefore = File.ReadAllText(path);
        var added = GitIgnoreAdvisor.AppendAcceptedRules(root, ["bin/", ".env"]);
        var after = File.ReadAllText(path);
        return before == stillBefore && added == 2 && preview == after &&
               preview.Contains("# Suggested by ZomniverseGitPet") && preview.Contains("bin/") && preview.Contains(".env");
    }
    finally { TryDelete(root); }
});

Check("environment preset ignores secrets but keeps simple and nested templates", () =>
{
    var option = GitIgnoreRuleLibrary.Presets.Single(item => item.Id == "env-files");
    return option.Rules.Contains(".env") &&
           option.Rules.Contains(".env.*") &&
           option.Rules.Contains("!.env.example") &&
           option.Rules.Contains("!.env.*.example") &&
           option.Rules.Contains("!.env.sample") &&
           option.Rules.Contains("!.env.*.sample") &&
           option.Rules.Contains("!.env.template") &&
           option.Rules.Contains("!.env.*.template") &&
           option.Rules.Contains("!.env.dist") &&
           option.Rules.Contains("!.env.*.dist");
});

Check("custom ignore builder creates friendly recursive patterns", () =>
{
    var folder = GitIgnoreRuleLibrary.CreateCustom(CustomIgnoreRuleKind.FolderName, "cache");
    var extension = GitIgnoreRuleLibrary.CreateCustom(CustomIgnoreRuleKind.FileExtension, ".tmp");
    var file = GitIgnoreRuleLibrary.CreateCustom(CustomIgnoreRuleKind.FileName, "secrets.json");
    var contains = GitIgnoreRuleLibrary.CreateCustom(CustomIgnoreRuleKind.NameContains, "LEGACY");
    return folder.Rules.SequenceEqual(["cache/"]) &&
           extension.Rules.SequenceEqual(["*.tmp"]) &&
           file.Rules.SequenceEqual(["secrets.json"]) &&
           contains.Rules.SequenceEqual(["**/*LEGACY*"]);
});

Check("friendly gitignore composer writes explanatory blocks exactly", () =>
{
    var root = CreateTempDirectory();
    try
    {
        var preview = ProjectGitIgnoreComposer.BuildPreview(
            root,
            ["/*", "!/.gitignore", "!/home/", "!/home/**"],
            [".env", ".env.*", "!.env.example", "**/*LEGACY*"]);
        var added = ProjectGitIgnoreComposer.Apply(
            root,
            ["/*", "!/.gitignore", "!/home/", "!/home/**"],
            [".env", ".env.*", "!.env.example", "**/*LEGACY*"]);
        var actual = File.ReadAllText(Path.Combine(root, ".gitignore"));
        return added == 8 && preview == actual &&
               preview.Contains("selected project scope") &&
               preview.Contains("managed project scope") &&
               preview.Contains("files and folders Git should leave alone") &&
               preview.Contains("Ignore real .env secret/configuration files") &&
               preview.Contains("name contains LEGACY/legacy");
    }
    finally { TryDelete(root); }
});

Check("combined scope rules keep internal exclusions before hygiene rules", () =>
{
    var root = CreateTempDirectory();
    try
    {
        var combined = new[] { "/*", "!/.gitignore", "!/CV/", "/CV/*", "!/CV/expertise/", "!/CV/expertise/**", "*.log" };
        var preview = GitIgnoreAdvisor.BuildPreviewContent(root, combined);
        var exclusion = preview.IndexOf("/CV/*", StringComparison.Ordinal);
        var reinclude = preview.IndexOf("!/CV/expertise/", StringComparison.Ordinal);
        var hygiene = preview.IndexOf("*.log", StringComparison.Ordinal);
        return exclusion >= 0 && reinclude > exclusion && hygiene > reinclude;
    }
    finally { TryDelete(root); }
});

Check("reconfigure replaces previous managed scope", () =>
{
    var root = CreateTempDirectory();
    try
    {
        ProjectGitIgnoreComposer.Apply(
            root,
            ["/*", "!/.gitignore", "!/home/", "!/home/**"],
            ["*.log"]);

        var preview = ProjectGitIgnoreComposer.BuildPreviewReplacingScope(
            root,
            ["/*", "!/.gitignore", "!/CV/", "!/CV/**"],
            ["*.log"]);
        var changed = ProjectGitIgnoreComposer.ApplyReplacingScope(
            root,
            ["/*", "!/.gitignore", "!/CV/", "!/CV/**"],
            ["*.log"]);
        var actual = File.ReadAllText(Path.Combine(root, ".gitignore"));

        return changed && preview == actual &&
               !actual.Contains("!/home/", StringComparison.Ordinal) &&
               actual.Contains("!/CV/", StringComparison.Ordinal) &&
               actual.Contains("# >>> ZomniverseGitPet managed project scope >>>", StringComparison.Ordinal);
    }
    finally { TryDelete(root); }
});

/* ==========================================================================
   PATCH: RESTORED SCOPE REGRESSION COVERAGE
   DATE.TIME: 2026-09-10 11:04 +03:00
   REASON:
   Verify managed scope survives reconfiguration and lazy tree restoration.
   ========================================================================== */
Check("managed scope round-trips selected directories and files", () =>
{
    var root = CreateTempDirectory();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "CV", "expertise"));
        File.WriteAllText(Path.Combine(root, "config.home.php"), "<?php");

        var plan = ProjectScopePlanner.Create(
            root,
            [new ProjectScopeEntry("CV/expertise", true), new ProjectScopeEntry("config.home.php", false)],
            trackEverything: false);
        var rules = ProjectScopePlanner.BuildIgnoreRules(plan);
        ProjectGitIgnoreComposer.Apply(root, rules, []);

        var restored = ProjectGitIgnoreComposer.ReadManagedScope(root);
        return restored is not null &&
               restored.Count == 2 &&
               restored.Any(entry => entry.IsDirectory && entry.RelativePath == "CV/expertise") &&
               restored.Any(entry => !entry.IsDirectory && entry.RelativePath == "config.home.php");
    }
    finally { TryDelete(root); }
});

Check("restored selected directory covers unloaded descendants", () =>
{
    var model = new ProjectScopeSelectionModel([new ProjectScopeEntry("home", true)]);
    return model.HasRestoredScope &&
           model.GetState("home", true, inheritedChecked: false) == ProjectScopeCheckState.Checked &&
           model.GetState("home/app", true, inheritedChecked: false) == ProjectScopeCheckState.Checked &&
           model.GetState("home/app/file.cs", false, inheritedChecked: false) == ProjectScopeCheckState.Checked &&
           model.GetState("other", true, inheritedChecked: false) == ProjectScopeCheckState.Unchecked;
});

Check("restored partial scope remains available while collapsed", () =>
{
    var model = new ProjectScopeSelectionModel(
        [new ProjectScopeEntry("CV/expertise", true), new ProjectScopeEntry("CV/readme.md", false)]);
    var preserved = model.GetPreservedSelections("CV");

    return model.GetState("CV", true, inheritedChecked: false) == ProjectScopeCheckState.Indeterminate &&
           model.GetState("CV/expertise", true, inheritedChecked: false) == ProjectScopeCheckState.Checked &&
           model.GetState("CV/applications", true, inheritedChecked: false) == ProjectScopeCheckState.Unchecked &&
           preserved.Count == 2;
});

Check("user subtree overrides replace restored lazy selections", () =>
{
    var model = new ProjectScopeSelectionModel([new ProjectScopeEntry("CV/expertise", true)]);

    model.SetSubtree("CV", false);
    var cleared = model.GetState("CV/expertise", true, inheritedChecked: false) == ProjectScopeCheckState.Unchecked;

    model.SetSubtree("CV", true);
    var selected = model.GetState("CV/applications", true, inheritedChecked: false) == ProjectScopeCheckState.Checked;

    model.SetSubtree("CV/applications", false);
    var parentPartial = model.GetState("CV", true, inheritedChecked: false) == ProjectScopeCheckState.Indeterminate;
    var childCleared = model.GetState("CV/applications", true, inheritedChecked: true) == ProjectScopeCheckState.Unchecked;
    var siblingStillSelected = model.GetState("CV/expertise", true, inheritedChecked: true) == ProjectScopeCheckState.Checked;

    return cleared && selected && parentPartial && childCleared && siblingStillSelected;
});

Check("major version suggestions advance generations without rewriting old tags", () =>
{
    var none = MajorUpdateCoordinator.SuggestMajorVersions([]);
    var existing = MajorUpdateCoordinator.SuggestMajorVersions(["v1.0.0", "v2.4.1", "notes"]);
    return none == (1, 2) && existing == (2, 3);
});

Check("github release links accept https and ssh origins", () =>
{
    var https = MajorUpdateCoordinator.TryGetGitHubWebUrl("https://github.com/example/project.git");
    var ssh = MajorUpdateCoordinator.TryGetGitHubWebUrl("git@github.com:example/project.git");
    var other = MajorUpdateCoordinator.TryGetGitHubWebUrl("https://gitlab.com/example/project.git");
    return https == "https://github.com/example/project" &&
           ssh == "https://github.com/example/project" &&
           other is null;
});

Check("github legacy release draft keeps tag and target explicit", () =>
{
    var url = MajorUpdateCoordinator.BuildGitHubReleaseDraftUrl(
        "https://github.com/example/project", "v1.0.0-legacy", "legacy/v1");
    return url.StartsWith("https://github.com/example/project/releases/new?", StringComparison.Ordinal) &&
           url.Contains("tag=v1.0.0-legacy", StringComparison.Ordinal) &&
           url.Contains("target=legacy%2Fv1", StringComparison.Ordinal);
});

/* ==========================================================================
   PATCH: IGNORED FILE SAVE REGRESSIONS
   DATE: 2026-09-11

   Verify provenance and exact force-track stage planning.
   ========================================================================== */
Check("normal changed file keeps non-force Save staging", () =>
{
    var args = IgnoredFileSavePolicy.BuildStageArguments(["src/app.cs"], force: false);
    return args.SequenceEqual(["add", "-A", "--", "src/app.cs"]);
});

Check("root gitignore provenance is retained", () =>
{
    var item = IgnoredFileSavePolicy.ParseCheckIgnore(string.Join('\0', ".gitignore", "37", "admin/", "admin/wanted.js") + "\0");
    return item is { IgnoreSource: ".gitignore", IgnoreLine: 37, Rule: "admin/", Path: "admin/wanted.js" };
});

Check("nested gitignore provenance is retained", () =>
{
    var item = IgnoredFileSavePolicy.ParseCheckIgnore(string.Join('\0', "src/.gitignore", "9", "cache/", "src/cache/item.txt") + "\0");
    return item is { IgnoreSource: "src/.gitignore", IgnoreLine: 9, Rule: "cache/" };
});

Check("info exclude provenance is retained", () =>
{
    var item = IgnoredFileSavePolicy.ParseCheckIgnore(string.Join('\0', ".git/info/exclude", "12", "cache/", "cache/local.txt") + "\0");
    return item is { IgnoreSource: ".git/info/exclude", IgnoreLine: 12, Rule: "cache/" };
});

Check("approved ignored file is force-added exactly", () =>
{
    var ignored = new IgnoredProjectFile("admin/wanted.js", ".gitignore", 37, "admin/");
    var plan = IgnoredFileSavePolicy.CreateStagePlan([], [ignored], [ignored.Path]);
    var args = IgnoredFileSavePolicy.BuildStageArguments(plan.ApprovedIgnoredFiles, force: true);
    return args.SequenceEqual(["add", "-f", "--", "admin/wanted.js"]);
});

Check("unticked ignored file is skipped while normal file remains", () =>
{
    var ignored = new IgnoredProjectFile("admin/wanted.js", ".gitignore", 37, "admin/");
    var plan = IgnoredFileSavePolicy.CreateStagePlan(["src/app.cs"], [ignored], []);
    return plan.NormalFiles.SequenceEqual(["src/app.cs"]) &&
           plan.ApprovedIgnoredFiles.Count == 0 &&
           plan.SkippedIgnoredFiles.SequenceEqual(["admin/wanted.js"]);
});

Check("multiple ignored files force-add only ticked paths", () =>
{
    var ignored = new[]
    {
        new IgnoredProjectFile("admin/a.js", ".gitignore", 37, "admin/"),
        new IgnoredProjectFile("admin/b.js", ".gitignore", 37, "admin/")
    };
    var plan = IgnoredFileSavePolicy.CreateStagePlan([], ignored, ["admin/b.js"]);
    return plan.ApprovedIgnoredFiles.SequenceEqual(["admin/b.js"]) &&
           plan.SkippedIgnoredFiles.SequenceEqual(["admin/a.js"]);
});

Check("force stage command never substitutes parent directory", () =>
{
    var ignored = new IgnoredProjectFile("admin/deep/wanted.js", ".gitignore", 37, "admin/");
    var plan = IgnoredFileSavePolicy.CreateStagePlan([], [ignored], [ignored.Path]);
    var args = IgnoredFileSavePolicy.BuildStageArguments(plan.ApprovedIgnoredFiles, force: true);
    return args.Contains("admin/deep/wanted.js") && !args.Contains("admin/") && !args.Contains("admin");
});

Check("cancel-equivalent empty approval produces no force command", () =>
{
    var ignored = new IgnoredProjectFile("cache/local.txt", ".git/info/exclude", 12, "cache/");
    var plan = IgnoredFileSavePolicy.CreateStagePlan([], [ignored], []);
    return IgnoredFileSavePolicy.BuildStageArguments(plan.ApprovedIgnoredFiles, force: true).Count == 0;
});

Check("tracked file beneath ignored parent stays normal", () =>
{
    var plan = IgnoredFileSavePolicy.CreateStagePlan(["admin/tracked.js"], [], []);
    return plan.NormalFiles.SequenceEqual(["admin/tracked.js"]) && plan.ApprovedIgnoredFiles.Count == 0;
});

Check("force stage preserves spaces in exact path", () =>
{
    var args = IgnoredFileSavePolicy.BuildStageArguments(["admin/my file.js"], force: true);
    return args[^1] == "admin/my file.js" && args.Count == 4;
});

Check("force stage preserves Unicode in exact path", () =>
{
    var args = IgnoredFileSavePolicy.BuildStageArguments(["дані/проєкт.txt"], force: true);
    return args[^1] == "дані/проєкт.txt" && args.Count == 4;
});

await CheckAsync("Git-native preflight reports all ignore sources before staging", async () =>
{
    var root = CreateTempDirectory();
    try
    {
        RunGit(root, "init");
        RunGit(root, "config", "user.name", "GitPet Test");
        RunGit(root, "config", "user.email", "gitpet@example.invalid");
        Directory.CreateDirectory(Path.Combine(root, "admin"));
        Directory.CreateDirectory(Path.Combine(root, "nested", "cache"));
        Directory.CreateDirectory(Path.Combine(root, "tracked-parent"));
        File.WriteAllText(Path.Combine(root, ".gitignore"), "admin/\ntracked-parent/\n");
        File.WriteAllText(Path.Combine(root, "nested", ".gitignore"), "cache/\n");
        File.WriteAllText(Path.Combine(root, "tracked-parent", "tracked.txt"), "before");
        RunGit(root, "add", "-f", "--", ".gitignore", "nested/.gitignore", "tracked-parent/tracked.txt");
        RunGit(root, "commit", "-m", "baseline");

        File.AppendAllText(Path.Combine(root, "tracked-parent", "tracked.txt"), " after");
        File.WriteAllText(Path.Combine(root, "admin", "wanted file.js"), "root ignore");
        File.WriteAllText(Path.Combine(root, "nested", "cache", "wanted.txt"), "nested ignore");
        File.AppendAllText(Path.Combine(root, ".git", "info", "exclude"), "\nlocal-cache/\n");
        Directory.CreateDirectory(Path.Combine(root, "local-cache"));
        File.WriteAllText(Path.Combine(root, "local-cache", "дані.txt"), "exclude ignore");

        var config = new AppConfig();
        config.RememberProject(root, root, "Scoped test", trackEverything: false,
        [
            new ProjectScopeEntry("admin", true),
            new ProjectScopeEntry("nested", true),
            new ProjectScopeEntry("local-cache", true),
            new ProjectScopeEntry("tracked-parent", true)
        ]);
        LogicalProjectScopeRuntime.Initialize(config);
        var service = new GitService(new AuditLog(Path.Combine(root, "audit.jsonl")));
        var result = await service.GetSavePreflightAsync(root);
        var stagedAfterPreflight = RunGit(root, "diff", "--cached", "--name-only");

        var passed = result.Success && string.IsNullOrWhiteSpace(stagedAfterPreflight) &&
                     result.NormalChangedFiles.Contains("tracked-parent/tracked.txt") &&
                     result.IgnoredChangedFiles.Any(item => item.Path == "admin/wanted file.js" && item.IgnoreSource.EndsWith(".gitignore")) &&
                     result.IgnoredChangedFiles.Any(item => item.Path == "nested/cache/wanted.txt" && item.IgnoreSource.EndsWith("nested/.gitignore")) &&
                     result.IgnoredChangedFiles.Any(item => item.Path == "local-cache/дані.txt" && item.IgnoreSource.Contains(".git/info/exclude"));
        if (!passed)
            throw new InvalidOperationException(
                $"success={result.Success}; error={result.Error}; staged={stagedAfterPreflight}; normal={string.Join('|', result.NormalChangedFiles)}; " +
                $"ignored={string.Join('|', result.IgnoredChangedFiles.Select(item => $"{item.Path}@{item.IgnoreSource}:{item.IgnoreLine}:{item.Rule}"))}");
        return true;
    }
    finally { TryDelete(root); }
});

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}
Console.WriteLine("All 48 ZomniverseGitPet tests passed.");
return 0;

void Check(string name, Func<bool> test)
{
    try { if (!test()) failures.Add("FAIL: " + name); }
    catch (Exception ex) { failures.Add($"FAIL: {name}: {ex.Message}"); }
}

async Task CheckAsync(string name, Func<Task<bool>> test)
{
    try { if (!await test()) failures.Add("FAIL: " + name); }
    catch (Exception ex) { failures.Add($"FAIL: {name}: {ex.Message}"); }
}

string RunGit(string workingDirectory, params string[] arguments)
{
    using var process = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    process.Start();
    var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException(output);
    return output.Trim();
}

string CreateTempDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "ZomniverseGitPet.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

void TryDelete(string path)
{
    try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
}
