using System.Diagnostics;

namespace Starter.Tests;

/// <summary>
/// Runs a PowerShell script or git the way the tests of scripts/ need it: from a given folder,
/// with the output captured, and with every GIT_* variable removed first. The tests also run
/// inside the real pre-commit hook, and an inherited GIT_DIR would point git at the real
/// repository instead of the throwaway one a test builds.
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
        ProcessStartInfo start = new(file)
        {
            WorkingDirectory = workDir,
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

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {file}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
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
