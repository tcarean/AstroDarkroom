using System.Diagnostics;

namespace Starter.Tests;

/// <summary>
/// Runs a PowerShell script or git the way the tests of scripts/ need it: from a given folder,
/// with the output captured, and with every GIT_* variable removed first; a hook also gets its
/// standard input and extra environment variables. The tests also run inside the real
/// pre-commit hook, and an inherited GIT_DIR would point git at the real repository instead of
/// the throwaway one a test builds.
/// </summary>
internal static class ScriptRunner
{
    /// <summary>The shell the scripts are written for: Windows PowerShell 5.1 where it exists, pwsh elsewhere.</summary>
    public static string Shell => OperatingSystem.IsWindows() ? "powershell" : "pwsh";

    public static (int ExitCode, string Output) RunScript(string workDir, string scriptPath, params string[] args)
    {
        string[] all = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, .. args];
        return Run(workDir, gitDir: null, Shell, all);
    }

    public static (int ExitCode, string Output) Run(string workDir, string? gitDir, string file, params string[] args)
    {
        return Run(workDir, gitDir, stdin: null, environment: null, file, args);
    }

    /// <summary>
    /// Runs a script the way Claude Code runs a hook: <paramref name="stdin"/> (the JSON payload)
    /// piped in and closed, <paramref name="environment"/> set on top of the inherited variables.
    /// </summary>
    public static (int ExitCode, string Output) RunHook(string workDir, string scriptPath, string stdin, IReadOnlyDictionary<string, string> environment)
    {
        return Run(workDir, gitDir: null, stdin, environment, Shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath);
    }

    private static (int ExitCode, string Output) Run(string workDir, string? gitDir, string? stdin, IReadOnlyDictionary<string, string>? environment, string file, params string[] args)
    {
        ProcessStartInfo start = new(file)
        {
            WorkingDirectory = workDir,
            RedirectStandardInput = stdin is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        foreach (string name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        if (gitDir is not null)
        {
            start.Environment["GIT_DIR"] = gitDir;
        }

        foreach ((string name, string value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {file}.");
        // Both outputs are read before stdin is written: a child that prints more than a pipe
        // holds before it reads its input would otherwise wait for us while we wait for it.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (stdin is not null)
        {
            // Without a byte order mark, as Claude Code writes it.
            using StreamWriter input = new(process.StandardInput.BaseStream, new System.Text.UTF8Encoding(false));
            input.Write(stdin);
        }

        process.WaitForExit();
        Task.WaitAll(stdout, stderr);
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>Deletes a folder git wrote into: its objects are read-only, which Directory.Delete refuses on Windows.</summary>
    public static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    /// <summary>Copies a folder with its subfolders.</summary>
    public static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}
