using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>在已打开的 Editor 内执行角色验收；仅消费明确的本地请求，不启动第二个 Unity。</summary>
[InitializeOnLoad]
public static class CharacterAuthoringValidationRunner
{
    static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../.utmp/authoring-validation"));
    static readonly string Request = Path.Combine(Output, "request.txt");
    static TestRunnerApi runner;
    static bool busy;
    static double nextPoll;
    static readonly StringBuilder log = new();
    static CharacterAuthoringValidationRunner()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "ready.txt"), DateTime.UtcNow.ToString("O"));
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (command == "run") Run();
        else if (command == "run-all") RunAll();
        else if (command == "plan-motion") MotionContentRepair.Run(false);
        else if (command == "repair-motion") MotionContentRepair.Run(true);
        else if (command == "complete-layout")
        {
            try
            {
                foreach (string role in new[] { "Anbi", "Monster", "Unagi", "UnagiEnemy" })
                    CharacterAssetLayout.EnsureBaseFolders(CharacterAssetLayout.DefaultParent + "/" + role);
                File.WriteAllText(Path.Combine(Output, "layout.txt"), "Completed base folders for four characters.");
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Output, "layout.txt"), e.ToString()); }
        }
        else if (command == "migrate-layout")
        {
            try
            {
                CharacterAssetMigration.Execute(JsonUtility.FromJson<CharacterAssetMigrationManifest>(
                    File.ReadAllText("docs/2026.9.29/CHARACTER_LAYOUT_MIGRATION.json")));
                File.WriteAllText(Path.Combine(Output, "migration.txt"), "Completed asset layout migration.");
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Output, "migration.txt"), e.ToString()); }
        }
        else File.WriteAllText(Path.Combine(Output, "status.txt"), "Rejected unknown request.");
    }

    /// <summary>只读审计生产内容并运行定向 EditMode 回归，保存完整 XML 与 Console 证据。</summary>
    [MenuItem("ACT/Character/Run Authoring Validation")]
    public static void Run() => RunSelected(false);

    /// <summary>结构整理后执行全部 EditMode 测试，覆盖跨程序集引用与网络、动作回归。</summary>
    public static void RunAll() => RunSelected(true);

    static void RunSelected(bool all)
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        busy = true; log.Clear();
        File.WriteAllText(Path.Combine(Output, "status.txt"), "Running " + DateTime.UtcNow.ToString("O"));
        Application.logMessageReceived += Capture;
        try
        {
            int failures = StructureValidationBatch.ValidateAll();
            File.WriteAllText(Path.Combine(Output, "audit.txt"), "Structure/content failures=" + failures + "\n" + log);
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Results());
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = all ? null : new[]
                {
                    "CharacterAuthoringCreationTests", "CharacterAuthoringTimingTests", "ActionGraphValidatorTests",
                    "CharacterAssetMigrationTests", "EnemyBehaviorTreeAssemblyMigrationTests",
                    "ActionEditorScenePreviewTests", "ActionEditorAlignmentTests", "ActionEditorViewportTests",
                    "ActionMotionBakeRangeTests",
                    "LocomotionIntegerClockTests", "ActionSimTests", "GameContentCatalogTests",
                    "GameContentBootstrapBoundaryTests", "ActionReplicationCatalogTests", "ServerContentManifestTests",
                    "NullAnimationPlaybackTests", "PartyExitFromHitTests", "AssistParryPipelineTests",
                    "AssistParryHitStopTests", "AssistParryHitStopCarryTests",
                },
            }));
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(Output, "status.txt"), "Failed to run: " + e);
            Finish();
        }
    }
    static void Capture(string condition, string trace, LogType type) => log.AppendLine($"[{type}] {condition}\n{trace}");
    static void Finish()
    {
        File.WriteAllText(Path.Combine(Output, "console.log"), log.ToString());
        Application.logMessageReceived -= Capture;
        busy = false;
        if (runner != null) UnityEngine.Object.DestroyImmediate(runner);
        runner = null;
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            File.WriteAllText(Path.Combine(Output, "results.xml"), result.ToXml().OuterXml);
            File.WriteAllText(Path.Combine(Output, "status.txt"), $"Completed: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}, status={result.TestStatus}");
            Finish();
        }
    }
}
