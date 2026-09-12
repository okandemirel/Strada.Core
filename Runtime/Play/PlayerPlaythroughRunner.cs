using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Strada.Core.Bootstrap;
using UnityEngine;

namespace Strada.Core.Play
{
    /// <summary>
    /// Plays the game inside the BUILT PLAYER and writes what happened.
    ///
    /// The editor play-through (Strada.MCP unity_playthrough) proves a project
    /// can be played; its frame timing is the editor's under -batchmode, which
    /// renders only at capture points and says nothing about the frame rate a
    /// person will see. This runner is the same drive — boot, resolve
    /// <see cref="IPlaythroughDriver"/>, start a session, act until it ends —
    /// inside the artifact unity_build_player produced, with real rendering
    /// and vsync, so a design document's "60 fps" can be measured where it
    /// applies. Nothing here knows a game's own names.
    ///
    /// Armed only by the command line, so a shipped player is unaffected:
    ///   Game.app/Contents/MacOS/Game -stradaPlaythrough /tmp/out/playthrough.json
    ///       [-stradaPlaythroughSession 1] [-stradaPlaythroughSessions all|1-3]
    ///       [-stradaPlaythroughMaxActions 60] [-stradaPlaythroughDeadline 45]
    ///       [-stradaPlaythroughBootDeadline 30] [-stradaCaptureDir /tmp/out]
    /// Writes the record (same field names as the editor test, plus medium
    /// "player"), captures a screenshot every 15 frames when a capture dir is
    /// given, and quits: exit 0 when every requested session reached an
    /// outcome, 30 otherwise.
    /// </summary>
    public sealed class PlayerPlaythroughRunner : MonoBehaviour
    {
        [Serializable]
        public class Record
        {
            public string medium = "player";
            public string scene;
            public int session;
            public string driverType = "Strada.Core.Play.IPlaythroughDriver";
            public string missing;
            public string phaseAfterBoot;
            public bool autoStarted;
            public bool startAccepted;
            public List<string> phasesSeen = new List<string>();
            public int actions;
            public string outcome;
            public bool reachedOutcome;
            public int framesCaptured;
            public float elapsedSeconds;
            public float bootSeconds = -1f;
            public float playSeconds;
            public int playFrames;
            public float worstFrameMs;
            public int sessionCount = -1;
            public List<SessionRecord> sessions = new List<SessionRecord>();
            public List<string> errors = new List<string>();
            public RuntimeDump runtime;
            public int targetFrameRate;
            public int vSyncCount;
            public int screenWidth;
            public int screenHeight;
        }


        [Serializable]
        public class RuntimeDump
        {
            public int renderers;
            public int worldRenderers;
            public int spriteRenderers;
            public int meshRenderers;
            public int canvases;
            public int particleSystems;
            public int audioSources;
            public int audioPlaying;
            public List<string> sprites = new List<string>();
            public List<string> meshes = new List<string>();
            public int primitiveMeshes;
        }

        /// <summary>What is actually on screen at the end of play: the file scan cannot see what code instantiates.</summary>
        static RuntimeDump DumpRuntime()
        {
            var d = new RuntimeDump();
            try
            {
                var seenSprites = new HashSet<string>();
                var seenMeshes = new HashSet<string>();
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    d.renderers++;
                    var sr = r as SpriteRenderer;
                    if (sr != null)
                    {
                        d.spriteRenderers++; d.worldRenderers++;
                        var name = sr.sprite != null ? sr.sprite.name : null;
                        if (!string.IsNullOrEmpty(name) && seenSprites.Add(name) && d.sprites.Count < 40) d.sprites.Add(name);
                        continue;
                    }
                    if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    {
                        d.meshRenderers++; d.worldRenderers++;
                        Mesh mesh = null;
                        var smr = r as SkinnedMeshRenderer;
                        if (smr != null) mesh = smr.sharedMesh;
                        else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                        var mname = mesh != null ? mesh.name : null;
                        if (!string.IsNullOrEmpty(mname))
                        {
                            if (mname == "Cube" || mname == "Sphere" || mname == "Capsule" || mname == "Cylinder" || mname == "Plane" || mname == "Quad") d.primitiveMeshes++;
                            if (seenMeshes.Add(mname) && d.meshes.Count < 40) d.meshes.Add(mname);
                        }
                        continue;
                    }
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) d.worldRenderers++;
                }
                d.canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length;
                d.particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length;
                foreach (var a in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    d.audioSources++;
                    if (a.isPlaying) d.audioPlaying++;
                }
            }
            catch (Exception) { /* a dump that fails leaves zeros; the frames still speak */ }
            return d;
        }

        [Serializable]
        public class SessionRecord
        {
            public int index;
            public bool startAccepted;
            public List<string> phasesSeen = new List<string>();
            public int actions;
            public string outcome;
            public bool reachedOutcome;
            public float seconds;
            public string lastPhase = "";
            /// <summary>The index this run ASKED for (the first session is 1).</summary>
            public int requestedIndex;
            /// <summary>
            /// True when the framework started this session itself, or when the
            /// game's IActiveSession confirmed the adopted session IS this one.
            /// False for a session adopted from auto-start that nothing could
            /// identify — its outcome certifies no particular content (Codex
            /// 2026-09-12 X).
            /// </summary>
            public bool identityVerified;
            /// <summary>The index the game reported as active, when it can report one; 0 otherwise.</summary>
            public int observedIndex;
        }

        const int MaxFrames = 40;
        const int FramesBetweenCaptures = 15;
        const int MaxSessionsPerRun = 12;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static int ArgInt(string name, int fallback)
        {
            int v;
            return int.TryParse(Arg(name), out v) ? v : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arm()
        {
            if (Application.isEditor) return;
            var jsonPath = Arg("-stradaPlaythrough");
            if (string.IsNullOrEmpty(jsonPath)) return;
            var go = new GameObject("Strada.PlayerPlaythroughRunner");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerPlaythroughRunner>().jsonPath = jsonPath;
        }

        string jsonPath;
        readonly Record record = new Record();

        void Start()
        {
            StartCoroutine(Run());
        }

        static List<int> SessionIndices(string spec, int defaultIndex, int catalogCount)
        {
            var result = new List<int>();
            if (string.IsNullOrEmpty(spec)) { result.Add(defaultIndex); return result; }
            spec = spec.Trim().ToLowerInvariant();
            if (spec == "all")
            {
                if (catalogCount <= 0) { result.Add(defaultIndex); return result; }
                for (var i = 1; i <= Math.Min(catalogCount, MaxSessionsPerRun); i++) result.Add(i);
                return result;
            }
            foreach (var part in spec.Split(','))
            {
                var range = part.Trim().Split('-');
                int a, b;
                if (range.Length == 2 && int.TryParse(range[0], out a) && int.TryParse(range[1], out b))
                {
                    for (var i = a; i <= b && result.Count < MaxSessionsPerRun; i++) result.Add(i);
                }
                else if (int.TryParse(part.Trim(), out a) && result.Count < MaxSessionsPerRun) result.Add(a);
            }
            if (result.Count == 0) result.Add(defaultIndex);
            return result;
        }

        static string Phase(IPlaythroughDriver driver)
        {
            try { return driver.Phase ?? ""; } catch { return ""; }
        }

        void Mirror(SessionRecord s)
        {
            record.session = s.index;
            record.startAccepted = s.startAccepted;
            record.actions = s.actions;
            record.outcome = s.outcome;
            record.reachedOutcome = s.reachedOutcome;
            record.phasesSeen = s.phasesSeen;
        }

        IEnumerator Run()
        {
            var started = Time.realtimeSinceStartup;
            var captureDir = Arg("-stradaCaptureDir");
            var maxActions = ArgInt("-stradaPlaythroughMaxActions", 60);
            var deadlineSeconds = ArgInt("-stradaPlaythroughDeadline", 45);
            var bootSeconds = ArgInt("-stradaPlaythroughBootDeadline", 30);
            record.session = ArgInt("-stradaPlaythroughSession", 1);
            record.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            record.targetFrameRate = Application.targetFrameRate;
            record.vSyncCount = QualitySettings.vSyncCount;
            record.screenWidth = Screen.width;
            record.screenHeight = Screen.height;
            Application.LogCallback watch = (condition, stackTrace, type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && record.errors.Count < 20)
                    record.errors.Add("[" + type + "] " + condition);
            };
            Application.logMessageReceived += watch;
            var exitCode = 30;
            try
            {
                var bootDeadline = started + bootSeconds;
                while (Time.realtimeSinceStartup < bootDeadline && GameBootstrapper.Services == null) yield return null;
                if (GameBootstrapper.Services == null)
                {
                    record.missing = "GameBootstrapper.Services stayed null for " + bootSeconds + " s — the entry scene holds no GameBootstrapper, or its config is unassigned, or a module threw while starting";
                    yield break;
                }
                record.bootSeconds = Time.realtimeSinceStartup - started;

                IPlaythroughDriver driver = null;
                var serviceDeadline = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < serviceDeadline && driver == null)
                {
                    try { GameBootstrapper.Services.TryGet(out driver); } catch { driver = null; }
                    if (driver == null) yield return null;
                }
                if (driver == null)
                {
                    record.missing = "the game registers no " + record.driverType + " — it cannot be played by the framework";
                    yield break;
                }
                var idleDeadline = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < idleDeadline) yield return null;
                record.phaseAfterBoot = Phase(driver);
                try { record.autoStarted = driver.IsSessionActive; } catch { record.autoStarted = false; }
                ISessionCatalog catalog = null;
                try { GameBootstrapper.Services.TryGet(out catalog); } catch { catalog = null; }
                if (catalog != null)
                {
                    try { record.sessionCount = catalog.SessionCount; }
                    catch (Exception e) { if (record.errors.Count < 20) record.errors.Add("[SessionCount] " + e.GetType().Name + ": " + e.Message); }
                }
                if (captureDir != null) Directory.CreateDirectory(captureDir);
                Capture(captureDir);

                var indices = SessionIndices(Arg("-stradaPlaythroughSessions"), record.session, record.sessionCount);
                var frame = 0;
                var wallPlaySeconds = 0f;
                var allEnded = true;
                for (var si = 0; si < indices.Count; si++)
                {
                    var s = new SessionRecord { index = indices[si], requestedIndex = indices[si] };
                    record.sessions.Add(s);
                    if (si == 0 && record.autoStarted)
                    {
                        // ADOPTING WHAT THE GAME ALREADY STARTED. Which session
                        // that is, only the game can say: without
                        // IActiveSession the record used to carry the index we
                        // asked for, so an auto-started level 1 certified
                        // level 7 (Codex 2026-09-12 X).
                        s.startAccepted = true;
                        IActiveSession active = null;
                        try { GameBootstrapper.Services.TryGet(out active); } catch { active = null; }
                        var observed = 0;
                        if (active != null)
                        {
                            try { observed = active.ActiveSession; }
                            catch (Exception e) { if (record.errors.Count < 20) record.errors.Add("[ActiveSession] " + e.GetType().Name + ": " + e.Message); }
                        }
                        s.observedIndex = observed;
                        // A PRESENT SERVICE REPORTING ZERO means NO session is
                        // running — the contract says so — and reading that as
                        // "cannot tell" verified the session we hoped for (Codex
                        // 2026-09-12 AA#3). Only a game that registers no identity
                        // service keeps the benefit of the doubt.
                        s.identityVerified = observed == s.requestedIndex;
                        if (observed > 0) s.index = observed;
                    }
                    else
                    {
                        if (si > 0) for (var settle = 0; settle < 5; settle++) yield return null;
                        try { s.startAccepted = driver.StartSession(s.index); }
                        catch (Exception e)
                        {
                            s.startAccepted = false;
                            if (record.errors.Count < 20) record.errors.Add("[StartSession " + s.index + "] " + e.GetType().Name + ": " + e.Message);
                        }
                    }
                    if (si == 0) Mirror(s);
                    yield return null;
                    if (!s.startAccepted) { s.outcome = "None"; if (si == 0) Mirror(s); allEnded = false; break; }
                    // WHAT IS ACTUALLY RUNNING, asked after the session is
                    // under way — for a session the framework started too.
                    // Accepting our own request as proof let a driver that
                    // clamps StartSession(7) to level 1 certify level 7
                    // (Codex 2026-09-12 Z#6). A game that registers no
                    // IActiveSession cannot be checked, so its own acceptance
                    // stands; one that contradicts the request is believed.
                    if (!(si == 0 && record.autoStarted))
                    {
                        IActiveSession activeNow = null;
                        try { GameBootstrapper.Services.TryGet(out activeNow); } catch { activeNow = null; }
                        var running = 0;
                        if (activeNow != null)
                        {
                            try { running = activeNow.ActiveSession; }
                            catch (Exception e) { if (record.errors.Count < 20) record.errors.Add("[ActiveSession] " + e.GetType().Name + ": " + e.Message); }
                        }
                        s.observedIndex = running;
                        // A PRESENT SERVICE REPORTING ZERO is no session running,
                        // not "cannot tell": accepting it verified a session the
                        // game had not started (Codex 2026-09-12 AA#3). Only a
                        // game with no identity service keeps its own acceptance.
                        s.identityVerified = activeNow == null || running == s.requestedIndex;
                        if (running > 0) s.index = running;
                    }

                    var playDeadline = Time.realtimeSinceStartup + deadlineSeconds;
                    var playStarted = Time.realtimeSinceStartup;
                    var last = "";
                    var skipDelta = true;
                    while (Time.realtimeSinceStartup < playDeadline)
                    {
                        if (!skipDelta)
                        {
                            var ms = Time.unscaledDeltaTime * 1000f;
                            record.playFrames++;
                            record.playSeconds += Time.unscaledDeltaTime;
                            if (ms > record.worstFrameMs) record.worstFrameMs = ms;
                        }
                        skipDelta = false;
                        var phase = Phase(driver);
                        if (phase != last) { s.phasesSeen.Add(phase); last = phase; }
                        PlaythroughOutcome outcome;
                        try { outcome = driver.Outcome; } catch { outcome = PlaythroughOutcome.None; }
                        if (outcome != PlaythroughOutcome.None)
                        {
                            s.outcome = outcome.ToString();
                            s.reachedOutcome = true;
                            break;
                        }
                        if (s.actions < maxActions)
                        {
                            try { if (driver.Act()) s.actions++; }
                            catch (Exception e) { if (record.errors.Count < 20) record.errors.Add("[Act] " + e.GetType().Name + ": " + e.Message); }
                        }
                        yield return null;
                        frame++;
                        if (frame % FramesBetweenCaptures == 0) { Capture(captureDir); skipDelta = true; }
                    }
                    s.seconds = Time.realtimeSinceStartup - playStarted;
                    wallPlaySeconds += s.seconds;
                    s.lastPhase = last;
                    if (!s.reachedOutcome) { s.outcome = "None"; allEnded = false; }
                    if (si == 0) Mirror(s);
                    if (!s.reachedOutcome) break;
                }
                if (record.playFrames == 0) record.playSeconds = wallPlaySeconds;
                record.runtime = DumpRuntime();
                Capture(captureDir);
                // Screenshots are written asynchronously at the end of a frame.
                yield return null;
                yield return null;
                if (allEnded && record.sessions.Count > 0) exitCode = 0;
            }
            finally
            {
                record.elapsedSeconds = Time.realtimeSinceStartup - started;
                Application.logMessageReceived -= watch;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
                    File.WriteAllText(jsonPath, JsonUtility.ToJson(record, true));
                }
                catch (Exception e) { Debug.LogWarning("player play-through record not written: " + e.Message); }
                // QUIT ON EVERY EXIT. `yield break` above — no bootstrap
                // services, no registered driver — runs this block and then
                // ends the iterator, so the player never quit and the tool
                // waited out its whole timeout before killing it (Codex
                // 2026-09-12 X).
                Application.Quit(exitCode);
            }
        }

        void Capture(string dir)
        {
            if (dir == null || record.framesCaptured >= MaxFrames) return;
            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "frame_" + record.framesCaptured.ToString("D5") + ".png"));
                record.framesCaptured++;
            }
            catch (Exception e)
            {
                if (record.errors.Count < 20) record.errors.Add("[Capture] " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
