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

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}
Console.WriteLine("All 3 ZomniverseGitPet tests passed.");
return 0;

void Check(string name, Func<bool> test)
{
    try { if (!test()) failures.Add("FAIL: " + name); }
    catch (Exception ex) { failures.Add($"FAIL: {name}: {ex.Message}"); }
}

