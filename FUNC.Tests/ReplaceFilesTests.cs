using System.Diagnostics;
using FUNC.Controllers;

namespace FUNC.Tests;

public sealed class ReplaceFilesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "func-replace-" + Guid.NewGuid().ToString("N"));
    private string Staging => Path.Combine(root, "staging");
    private string Target => Path.Combine(root, "target");

    public ReplaceFilesTests()
    {
        Directory.CreateDirectory(Staging);
        Directory.CreateDirectory(Target);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public async Task ReplacesAndAddsNestedFiles()
    {
        Write(Path.Combine(Target, "bin", "goal"), "old goal");
        Write(Path.Combine(Target, "keep.txt"), "untouched");
        Write(Path.Combine(Staging, "bin", "goal"), "new goal");
        Write(Path.Combine(Staging, "genesis", "mainnet", "genesis.json"), "{}");

        await GoalController.ReplaceFiles(Staging, Target);

        Assert.Equal("new goal", File.ReadAllText(Path.Combine(Target, "bin", "goal")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(Target, "genesis", "mainnet", "genesis.json")));
        Assert.Equal("untouched", File.ReadAllText(Path.Combine(Target, "keep.txt")));
        Assert.Empty(Directory.EnumerateFiles(Target, "*.func-old*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AFailedMove_PutsBackWhatWasReplaced()
    {
        Write(Path.Combine(Target, "bin", "a"), "old a");
        Write(Path.Combine(Target, "bin", "c"), "old c");
        // A directory where the new file "b" should go makes its move fail.
        Directory.CreateDirectory(Path.Combine(Target, "bin", "b"));
        foreach (string n in new[] { "a", "b", "c" }) Write(Path.Combine(Staging, "bin", n), "new " + n);

        await Assert.ThrowsAnyAsync<IOException>(() => GoalController.ReplaceFiles(Staging, Target, attempts: 1));

        Assert.Equal("old a", File.ReadAllText(Path.Combine(Target, "bin", "a")));
        Assert.Equal("old c", File.ReadAllText(Path.Combine(Target, "bin", "c")));
        Assert.Empty(Directory.EnumerateFiles(Target, "*.func-old*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ReplacesARunningExecutable()
    {
        if (!OperatingSystem.IsWindows()) return;  // Unix can always replace a running binary

        // A copy of ping.exe stands in for a diagcfg.exe that a status check just started.
        string exe = Path.Combine(Target, "bin", "diagcfg.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), exe);
        Write(Path.Combine(Staging, "bin", "diagcfg.exe"), "new diagcfg");

        using var proc = Process.Start(new ProcessStartInfo(exe, "-n 30 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        try
        {
            Assert.Throws<IOException>(() => File.Copy(Path.Combine(Staging, "bin", "diagcfg.exe"), exe, overwrite: true));

            await GoalController.ReplaceFiles(Staging, Target, attempts: 1);

            Assert.Equal("new diagcfg", File.ReadAllText(exe));
            Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(exe)!, "*.func-old*"));  // still running
        }
        finally
        {
            proc.Kill();
            proc.WaitForExit();
        }

        // The next install clears the leftover once nothing runs it.
        Write(Path.Combine(Staging, "bin", "diagcfg.exe"), "newer diagcfg");
        await GoalController.ReplaceFiles(Staging, Target);
        Assert.Equal("newer diagcfg", File.ReadAllText(exe));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(exe)!, "*.func-old*"));
    }
}
