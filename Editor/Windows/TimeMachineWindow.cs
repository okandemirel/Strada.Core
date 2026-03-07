using System;
using System.Collections.Generic;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Storage;
using Strada.Core.ECS.World;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Time Machine window for recording and replaying ECS World states.
    /// Allows stepping back in time to debug logic.
    /// </summary>
    public class TimeMachineWindow : EditorWindow
    {
        private bool _isRecording;
        private int _maxSnapshots = 600;
        private List<WorldSnapshot> _snapshots = new List<WorldSnapshot>();
        private int _playbackFrame = -1;
        private bool _isReplaying;
        private bool _isPlayingRecording;

        private WorldSnapshot _liveSnapshot;
        private float _playbackSpeed = 1.0f;
        private double _lastPlaybackTime;

        public static void ShowWindow()
        {
            var window = GetWindow<TimeMachineWindow>("Time Machine");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _snapshots.Clear();
            _liveSnapshot = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _isRecording = false;
                _isReplaying = false;
                _isPlayingRecording = false;
                _snapshots.Clear();
                _liveSnapshot = null;
                _playbackFrame = -1;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_isRecording && !_isReplaying)
            {
                RecordSnapshot();
            }

            if (_isPlayingRecording && _isReplaying)
            {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - _lastPlaybackTime > (0.016f / _playbackSpeed))              {
                    _lastPlaybackTime = currentTime;
                    Step(1);
                    
                    if (_playbackFrame >= _snapshots.Count - 1)
                    {
                        _isPlayingRecording = false;
                    }
                }
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use Time Machine.", MessageType.Info);
                return;
            }

            DrawToolbar();
            DrawTimeline();
            DrawSnapshotDetails();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(_isRecording ? "Stop Recording" : "Start Recording", EditorStyles.toolbarButton))
            {
                _isRecording = !_isRecording;
                if (_isRecording)
                {
                    if (_isReplaying)
                    {
                        RestoreLiveState();
                    }
                    _isReplaying = false;
                    _isPlayingRecording = false;
                }
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                _snapshots.Clear();
                _playbackFrame = -1;
                _isReplaying = false;
                _isPlayingRecording = false;
                _liveSnapshot = null;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Snapshots: {_snapshots.Count}/{_maxSnapshots}");

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimeline()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Timeline", EditorStyles.boldLabel);

            if (_snapshots.Count > 0)
            {
                int maxFrame = _snapshots.Count - 1;
                int currentFrame = _playbackFrame == -1 ? maxFrame : _playbackFrame;

                var newFrame = EditorGUILayout.IntSlider(currentFrame, 0, maxFrame);
                if (newFrame != currentFrame)
                {
                    StartReplayIfNeeded();
                    _playbackFrame = newFrame;
                    _isPlayingRecording = false;
                    RestoreSnapshot(_snapshots[_playbackFrame]);
                }

                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("<<", GUILayout.Width(30)))
                {
                    StartReplayIfNeeded();
                    Step(-10);
                }
                
                if (GUILayout.Button("<", GUILayout.Width(30)))
                {
                    StartReplayIfNeeded();
                    Step(-1);
                }

                if (GUILayout.Button(_isPlayingRecording ? "||" : "►", GUILayout.Width(30)))
                {
                    StartReplayIfNeeded();
                    _isPlayingRecording = !_isPlayingRecording;
                    _lastPlaybackTime = EditorApplication.timeSinceStartup;
                }

                if (GUILayout.Button(">", GUILayout.Width(30)))
                {
                    StartReplayIfNeeded();
                    Step(1);
                }
                
                if (GUILayout.Button(">>", GUILayout.Width(30)))
                {
                    StartReplayIfNeeded();
                    Step(10);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(_isReplaying ? "Return to Live" : "Live", GUILayout.Width(100)))
                {
                    if (_isReplaying)
                    {
                        RestoreLiveState();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No snapshots recorded.");
            }

            EditorGUILayout.EndVertical();
        }

        private void StartReplayIfNeeded()
        {
            if (!_isReplaying)
            {
                _isReplaying = true;
                _isRecording = false;
                
                if (World.Current != null)
                {
                    _liveSnapshot = new WorldSnapshot();
                    _liveSnapshot.Capture(World.Current);
                }
                
                if (_playbackFrame == -1)
                {
                    _playbackFrame = _snapshots.Count - 1;
                }
            }
        }

        private void RestoreLiveState()
        {
            _isReplaying = false;
            _isPlayingRecording = false;
            _playbackFrame = -1;

            if (_liveSnapshot != null && World.Current != null)
            {
                _liveSnapshot.Restore(World.Current);
                _liveSnapshot = null;
            }
        }

        private void Step(int frames)
        {
            if (_snapshots.Count == 0) return;
            
            int current = _playbackFrame == -1 ? _snapshots.Count - 1 : _playbackFrame;
            int target = Mathf.Clamp(current + frames, 0, _snapshots.Count - 1);
            
            _playbackFrame = target;
            RestoreSnapshot(_snapshots[_playbackFrame]);
        }

        private void DrawSnapshotDetails()
        {
            if (_playbackFrame == -1 || _playbackFrame >= _snapshots.Count) return;

            var snapshot = _snapshots[_playbackFrame];
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Snapshot Frame: {_playbackFrame}", EditorStyles.boldLabel);
            GUILayout.Label($"Timestamp: {snapshot.Timestamp:F3}");
            GUILayout.Label($"Entities: {snapshot.EntityCount}");
            EditorGUILayout.EndVertical();
        }

        private void RecordSnapshot()
        {
            if (World.Current == null) return;

            var snapshot = new WorldSnapshot();
            snapshot.Capture(World.Current);
            
            _snapshots.Add(snapshot);
            if (_snapshots.Count > _maxSnapshots)
            {
                _snapshots.RemoveAt(0);
                if (_playbackFrame > 0) _playbackFrame--;
            }
        }

        private void RestoreSnapshot(WorldSnapshot snapshot)
        {
            if (World.Current == null) return;
            snapshot.Restore(World.Current);
        }
    }

    public class WorldSnapshot
    {
        public double Timestamp;
        public int EntityCount;
        
        public int NextEntityIndex;
        public int[] ActiveIndices;
        public int[] Versions;

        private Dictionary<int, Dictionary<Type, object>> _entityData = new Dictionary<int, Dictionary<Type, object>>();

        public void Capture(World world)
        {
            Timestamp = EditorApplication.timeSinceStartup;
            var entityManager = world.EntityManager;
            EntityCount = entityManager.EntityCount;

            entityManager.CaptureState(out NextEntityIndex, out ActiveIndices, out Versions);

            foreach (var entityId in ActiveIndices)
            {
                var components = new Dictionary<Type, object>();
                foreach (var type in entityManager.Store.GetComponentTypes())
                {
                    if (entityManager.Store.HasComponent(entityId, type))
                    {
                        var value = entityManager.Store.GetComponentBoxed(entityId, type);
                        if (value != null)
                        {
                            components[type] = value;
                        }
                    }
                }
                _entityData[entityId] = components;
            }
        }

        public void Restore(World world)
        {
            var entityManager = world.EntityManager;
            
            entityManager.RestoreState(NextEntityIndex, ActiveIndices, Versions);

            foreach (var kvp in _entityData)
            {
                int entityId = kvp.Key;
                var components = kvp.Value;
                
                foreach (var compKvp in components)
                {
                    var type = compKvp.Key;
                    var value = compKvp.Value;
                    entityManager.Store.SetComponentBoxed(entityId, type, value);
                }
            }
        }
    }
}
