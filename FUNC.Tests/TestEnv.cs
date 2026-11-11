using System.Runtime.CompilerServices;

namespace FUNC.Tests;

// FUNC keeps its state under Utils.appDataDir, which is fixed when the first FUNC type
// loads. Point it at a throwaway directory before any test touches FUNC.
internal static class TestEnv
{
    public static readonly string DataDir = Path.Combine(Path.GetTempPath(), "func-tests-" + Guid.NewGuid().ToString("N"));

#pragma warning disable CA2255 // a module initializer is the only hook that runs early enough
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Init()
    {
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("FUNC_DATA_DIR", DataDir);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(DataDir, true); } catch { }
        };
    }

    // A clean data dir for a node, as Node.DataDir resolves it.
    public static string ResetNodeDir(string name)
    {
        string dir = Path.Combine(DataDir, name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }
}

// FUNC's state is static and file-backed, so tests that touch it must not run in parallel.
[CollectionDefinition("AppData", DisableParallelization = true)]
public class AppDataCollection { }
