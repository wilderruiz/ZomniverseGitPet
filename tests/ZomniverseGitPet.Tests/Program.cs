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

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}
Console.WriteLine("All 14 ZomniverseGitPet tests passed.");
return 0;

void Check(string name, Func<bool> test)
{
    try { if (!test()) failures.Add("FAIL: " + name); }
    catch (Exception ex) { failures.Add($"FAIL: {name}: {ex.Message}"); }
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
