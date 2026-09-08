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

Check("gitignore advisor finds common and security candidates", () =>
{
    var root = CreateTempDirectory();
    try
    {
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        File.WriteAllText(Path.Combine(root, ".env"), "SECRET=test");
        var suggestions = GitIgnoreAdvisor.Suggest(root);
        return suggestions.Any(item => item.Rule == "bin/" && item.DefaultSelected) &&
               suggestions.Any(item => item.Rule == "node_modules/" && item.DefaultSelected) &&
               suggestions.Any(item => item.Rule == ".env" && item.Confidence == GitIgnoreConfidence.Security);
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

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}
Console.WriteLine("All 7 ZomniverseGitPet tests passed.");
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
