using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Strada.Core.Editor.Headless
{
    /// <summary>
    /// Build a player with no Editor open.
    ///
    /// The framework's tooling could assemble scenes, run tests and play the
    /// game headlessly, and the only build entry point still needed a live
    /// Editor bridge — so "delivery" meant an Editor project, never a runnable
    /// artifact (audited 2026-09-10). This is the counterpart of
    /// <see cref="StradaSceneBuilder"/> for the build step:
    ///
    ///   Unity -batchmode -nographics -projectPath &lt;project&gt;
    ///         -executeMethod Strada.Core.Editor.Headless.StradaPlayerBuilder.Build
    ///         -stradaTarget android|ios|webgl|windows|macos|linux
    ///         -stradaOutput &lt;dir&gt; -stradaResult result.json -logFile build.log
    ///
    /// No -quit: the builder owns EditorApplication.Exit and its code carries
    /// the verdict. The result file names what was built, how big it is, how
    /// long it took, and every error the build report holds.
    /// </summary>
    public static class StradaPlayerBuilder
    {
        private const int ExitOk = 0;
        private const int ExitArgError = 20;
        private const int ExitNoScenes = 21;
        private const int ExitBuildFailed = 22;
        private const int ExitTargetUnsupported = 23;

        private static readonly List<string> Errors = new List<string>();
        private static int _warnings;

        public static void Build()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            string target = ReadArg("-stradaTarget");
            string output = ReadArg("-stradaOutput");
            string resultPath = ReadArg("-stradaResult");
            var started = DateTime.UtcNow;
            int code;
            string outputPath = null;
            long sizeBytes = 0;
            var scenes = new List<string>();
            string resolvedTarget = target ?? string.Empty;
            try
            {
                code = Run(target, output, out outputPath, out sizeBytes, scenes, out resolvedTarget);
            }
            catch (Exception e)
            {
                Errors.Add($"{e.GetType().Name}: {e.Message}");
                code = ExitBuildFailed;
            }
            WriteResult(resultPath, code, resolvedTarget, outputPath, sizeBytes, (long)(DateTime.UtcNow - started).TotalMilliseconds, scenes);
            EditorApplication.Exit(code);
        }

        private static int Run(string targetName, string output, out string outputPath, out long sizeBytes, List<string> scenes, out string resolvedTarget)
        {
            outputPath = null;
            sizeBytes = 0;
            resolvedTarget = targetName ?? string.Empty;
            BuildTarget target;
            BuildTargetGroup group;
            if (!ResolveTarget(targetName, out target, out group, out resolvedTarget))
            {
                Errors.Add($"unknown target '{targetName}' (android, ios, webgl, windows, macos, linux; empty = the project's active target)");
                return ExitArgError;
            }
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Errors.Add($"build target {target} is not supported by this Editor install — the {target} build support module is missing");
                return ExitTargetUnsupported;
            }
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);
            if (scenes.Count == 0)
            {
                Errors.Add("Build Settings enables no scene — there is nothing to build");
                return ExitNoScenes;
            }
            var dir = string.IsNullOrEmpty(output) ? Path.Combine(Directory.GetCurrentDirectory(), "Builds", resolvedTarget) : output;
            Directory.CreateDirectory(dir);
            var productName = string.IsNullOrEmpty(PlayerSettings.productName) ? "Game" : PlayerSettings.productName;
            var location = LocationFor(target, dir, productName);
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = location,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            outputPath = summary.outputPath;
            sizeBytes = (long)summary.totalSize;
            _warnings = summary.totalWarnings;
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Assert)
                        Errors.Add($"{step.name}: {message.content}");
            if (summary.result != BuildResult.Succeeded)
            {
                if (Errors.Count == 0) Errors.Add($"build result {summary.result} with {summary.totalErrors} error(s) — see the Unity log");
                return ExitBuildFailed;
            }
            if (sizeBytes == 0 && !string.IsNullOrEmpty(outputPath)) sizeBytes = SizeOnDisk(outputPath);
            return ExitOk;
        }

        private static bool ResolveTarget(string name, out BuildTarget target, out BuildTargetGroup group, out string resolved)
        {
            var key = (name ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                target = EditorUserBuildSettings.activeBuildTarget;
                group = BuildPipeline.GetBuildTargetGroup(target);
                resolved = target.ToString().ToLowerInvariant();
                return true;
            }
            switch (key)
            {
                case "android": target = BuildTarget.Android; group = BuildTargetGroup.Android; resolved = "android"; return true;
                case "ios": target = BuildTarget.iOS; group = BuildTargetGroup.iOS; resolved = "ios"; return true;
                case "webgl": target = BuildTarget.WebGL; group = BuildTargetGroup.WebGL; resolved = "webgl"; return true;
                case "windows": target = BuildTarget.StandaloneWindows64; group = BuildTargetGroup.Standalone; resolved = "windows"; return true;
                case "macos": target = BuildTarget.StandaloneOSX; group = BuildTargetGroup.Standalone; resolved = "macos"; return true;
                case "linux": target = BuildTarget.StandaloneLinux64; group = BuildTargetGroup.Standalone; resolved = "linux"; return true;
                default: target = BuildTarget.NoTarget; group = BuildTargetGroup.Unknown; resolved = key; return false;
            }
        }

        private static string LocationFor(BuildTarget target, string dir, string productName)
        {
            switch (target)
            {
                case BuildTarget.Android: return Path.Combine(dir, productName + (EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk"));
                case BuildTarget.iOS: return dir; // an Xcode project folder
                case BuildTarget.WebGL: return dir; // an index.html folder
                case BuildTarget.StandaloneWindows64: return Path.Combine(dir, productName + ".exe");
                case BuildTarget.StandaloneOSX: return Path.Combine(dir, productName + ".app");
                case BuildTarget.StandaloneLinux64: return Path.Combine(dir, productName + ".x86_64");
                default: return Path.Combine(dir, productName);
            }
        }

        private static long SizeOnDisk(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            }
            catch { return 0; }
        }

        private static string ReadArg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        private static void WriteResult(string path, int code, string target, string outputPath, long sizeBytes, long durationMs, List<string> scenes)
        {
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.Append("{\"built\":").Append(code == ExitOk ? "true" : "false");
            sb.Append(",\"exitCode\":").Append(code);
            sb.Append(",\"target\":").Append(Quote(target));
            sb.Append(",\"outputPath\":").Append(Quote(outputPath ?? string.Empty));
            sb.Append(",\"sizeBytes\":").Append(sizeBytes);
            sb.Append(",\"durationMs\":").Append(durationMs);
            sb.Append(",\"warnings\":").Append(_warnings);
            sb.Append(",\"scenes\":[").Append(string.Join(",", scenes.Select(Quote))).Append(']');
            sb.Append(",\"errors\":[").Append(string.Join(",", Errors.Select(Quote))).Append("]}");
            try { File.WriteAllText(path, sb.ToString()); } catch { /* the exit code still carries the verdict */ }
        }

        private static string Quote(string s) =>
            "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}
