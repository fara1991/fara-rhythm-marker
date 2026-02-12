using System;
using System.Collections.Generic;
using FaraRhythmMarker.Configuration;
using FaraRhythmMarker.Models;
using FaraRhythmMarker.Views;
using UnityEngine;
using Zenject;

namespace FaraRhythmMarker.Controllers
{
    /// <summary>
    /// Main controller that coordinates the Model and View during gameplay
    /// </summary>
    internal class RhythmMarkerController : IInitializable, ITickable, IDisposable
    {
        private readonly AudioTimeSyncController _audioTimeSyncController;
#if BS_1_29_1
        private readonly IDifficultyBeatmap _difficultyBeatmap;
#else
        private readonly BeatmapLevel? _beatmapLevel;
#endif
        private readonly GameplayCoreSceneSetupData _sceneSetupData;
        private readonly PlayerTransforms _playerTransforms;
        private readonly IAudioTimeSource _audioTimeSource;
        private readonly IReadonlyBeatmapData? _beatmapData;

        private readonly BeatmapObjectSpawnController.InitData? _spawnInitData;
        private readonly BeatmapObjectSpawnController? _spawnController;
        private readonly BeatTimingModel _model;
        private readonly MarkerView _view;
        private bool _songStarted = false;
        private float _songStartTime = 0f;

        public RhythmMarkerController(
            AudioTimeSyncController audioTimeSyncController,
#if BS_1_29_1
            IDifficultyBeatmap difficultyBeatmap,
#else
            [InjectOptional] BeatmapLevel? beatmapLevel,
#endif
            GameplayCoreSceneSetupData sceneSetupData,
            PlayerTransforms playerTransforms,
            IAudioTimeSource audioTimeSource,
            [InjectOptional] IReadonlyBeatmapData? beatmapData = null,
            [InjectOptional] BeatmapObjectSpawnController.InitData? spawnInitData = null,
            [InjectOptional] BeatmapObjectSpawnController? spawnController = null)
        {
            _audioTimeSyncController = audioTimeSyncController;
#if BS_1_29_1
            _difficultyBeatmap = difficultyBeatmap;
#else
            _beatmapLevel = beatmapLevel;
#endif
            _sceneSetupData = sceneSetupData;
            _playerTransforms = playerTransforms;
            _audioTimeSource = audioTimeSource;
            _beatmapData = beatmapData;
            _spawnInitData = spawnInitData;
            _spawnController = spawnController;

            _model = new BeatTimingModel();
            _view = new MarkerView();
        }

        public void Initialize()
        {
            Plugin.Log.Info($"RhythmMarkerController.Initialize: Enabled={PluginConfig.Instance.Enabled}, ColorMode={PluginConfig.Instance.ColorMode}, BeatDivision={PluginConfig.Instance.BeatDivision}");
            Plugin.Log.Info($"Config instance hash: {PluginConfig.Instance.GetHashCode()}");

            if (!PluginConfig.Instance.Enabled)
            {
                Plugin.Log.Info("RhythmMarkerController: Disabled by config, skipping initialization");
                if (_view.IsInitialized)
                {
                    _view.Dispose();
                }
                return;
            }

            float njs = InitializeNjsAndHitPosition();

            // Apply Z offset from config
            float configZOffset = PluginConfig.Instance.MarkerZOffset;
            _view.HitZOffset += configZOffset;
            Plugin.Log.Info($"Applied config MarkerZOffset: {configZOffset}. Final HitZOffset: {_view.HitZOffset}");

            // Initialize model
#if BS_1_29_1
            float bpm = _difficultyBeatmap.level.beatsPerMinute;
            float songDuration = _difficultyBeatmap.level.songDuration;
#else
            float bpm = _beatmapLevel?.beatsPerMinute ?? 120f;
            float songDuration = _audioTimeSyncController.songLength;
#endif

            var bpmChanges = ExtractBpmChanges(bpm);

            _model.Initialize(bpm);
            _model.SetBpmChanges(bpmChanges, songDuration);
            _model.OnBeat += OnBeatTriggered;

            _view.Initialize(_audioTimeSyncController, _playerTransforms, njs, bpm, _model.GetBpmAtTime);

            Plugin.Log.Info($"RhythmMarkerController initialized: {_model.GetMarkersPerMinute()} markers/min, HitZOffset: {_view.HitZOffset}, NJS: {njs}, BPM Changes: {bpmChanges.Count}");
        }

        /// <summary>
        /// Initializes NJS and hit position based on play space origin (Z=0)
        /// </summary>
        private float InitializeNjsAndHitPosition()
        {
            float njs = 12f;
            try
            {
#if BS_1_29_1
                njs = _difficultyBeatmap.noteJumpMovementSpeed;
#else
                if (_spawnInitData != null)
                {
                    njs = _spawnInitData.noteJumpMovementSpeed;
                }
#endif

                if (_spawnInitData != null)
                {
                    Plugin.Log.Info($"SpawnInitData: noteJumpValue={_spawnInitData.noteJumpValue}, noteJumpValueType={_spawnInitData.noteJumpValueType}, NJS={_spawnInitData.noteJumpMovementSpeed}");
                }

                // Use play space origin (Z=0) as the marker hit position
                // The platform is always centered at the origin
                _view.HitZOffset = 0f;
                Plugin.Log.Info($"Using play space origin. HitZOffset: {_view.HitZOffset}");

                TryGetPlatformXOffset();
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get NJS or player position: {ex.Message}. Using defaults.");
                _view.HitZOffset = 0f;
            }

            return njs;
        }

        private void OnBeatTriggered(int colorIndex, float hitTime)
        {
            _view.SpawnMarker(colorIndex, hitTime);
        }

        public void Tick()
        {
            if (!PluginConfig.Instance.Enabled || !_view.IsInitialized)
                return;

            // Update model with current song time
            float songTime = _audioTimeSyncController.songTime;

            // Detect song start
            if (!_songStarted && songTime > 0)
            {
                _songStarted = true;
                // Set current songTime as offset so song start is beat 0
                _songStartTime = songTime;
                _model.SetStartTimeOffset(_songStartTime);
                Plugin.Log.Info($"Song start detected at songTime: {_songStartTime}. This is now beat 0.");
            }

            // Only calculate beats after song start
            if (_songStarted)
            {
                _model.Update(songTime);
            }

            // Update markers and side lights
            _view.UpdateMarkers();
        }

        public void Dispose()
        {
            Plugin.Log.Info("RhythmMarkerController.Dispose called");
            _model.OnBeat -= OnBeatTriggered;
            _view.Dispose();
        }

        /// <summary>
        /// Gets X width from platform for marker placement
        /// </summary>
        private void TryGetPlatformXOffset()
        {
            try
            {
                var platformObj = FindPlatformGameObject();
                if (platformObj != null)
                {
                    var renderer = platformObj.GetComponent<Renderer>() ?? platformObj.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        var bounds = renderer.bounds;
                        float platformHalfWidth = bounds.extents.x;
                        _view.PlatformXOffset = platformHalfWidth;
                        Plugin.Log.Info($"Set PlatformXOffset to platform half-width: {platformHalfWidth}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get platform X offset: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the platform GameObject
        /// </summary>
        private GameObject? FindPlatformGameObject()
        {
            string[] platformNames = new[]
            {
                "PlayersPlace",
                "PlayersPlaceShadow",
                "Feet",
                "Platform",
                "MenuPlayersPlace"
            };

            foreach (var name in platformNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    return obj;
                }
            }

            // If not found by name, search all objects
            var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name.Contains("PlayersPlace") || obj.name.Contains("Platform"))
                {
                    if (obj.GetComponent<Renderer>() != null || obj.GetComponentInChildren<Renderer>() != null)
                    {
                        return obj;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts BPM change events from beatmap data
        /// </summary>
        /// <param name="baseBpm">Base BPM</param>
        /// <returns>List of BPM change events</returns>
        private List<BpmChangeEvent> ExtractBpmChanges(float baseBpm)
        {
            var bpmChanges = new List<BpmChangeEvent>();

            try
            {
                // First try the injected beatmapData
                IReadonlyBeatmapData? beatmapData = _beatmapData;

#if BS_1_29_1
                // In 1.29.1, get from IDifficultyBeatmap
                if (beatmapData == null && _difficultyBeatmap != null)
                {
                    try
                    {
                        // Access beatmapData property
                        var beatmapDataProp = _difficultyBeatmap.GetType().GetProperty("beatmapData");
                        if (beatmapDataProp != null)
                        {
                            beatmapData = beatmapDataProp.GetValue(_difficultyBeatmap) as IReadonlyBeatmapData;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Warn($"Failed to get beatmapData from IDifficultyBeatmap: {ex.Message}");
                    }
                }
#endif

                if (beatmapData == null)
                {
                    Plugin.Log.Info("BeatmapData not available, using base BPM only");
                    return bpmChanges;
                }

                Plugin.Log.Info($"BeatmapData found: {beatmapData.GetType().FullName}");

                // Find BPM change events (using reflection for compatibility)
                ExtractBpmChangesFromBeatmapData(beatmapData, baseBpm, bpmChanges);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to extract BPM changes: {ex.Message}");
            }

            return bpmChanges;
        }

        /// <summary>
        /// Extracts BPM change events from beatmap data (using reflection)
        /// </summary>
        private void ExtractBpmChangesFromBeatmapData(IReadonlyBeatmapData beatmapData, float baseBpm, List<BpmChangeEvent> bpmChanges)
        {
            var rawBpmChanges = new List<(float time, float bpm)>();

            try
            {
                // Try multiple methods to get BPM changes
                TryExtractFromAllBeatmapDataItems(beatmapData, baseBpm, rawBpmChanges);

                if (rawBpmChanges.Count == 0)
                    TryExtractFromGetBeatmapDataItems(beatmapData, baseBpm, rawBpmChanges);

                if (rawBpmChanges.Count == 0)
                    TryExtractFromBeatmapEventsData(beatmapData, baseBpm, rawBpmChanges);

                ProcessBpmChanges(rawBpmChanges, baseBpm, bpmChanges);
                Plugin.Log.Info($"Extracted {bpmChanges.Count} BPM change events");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Error extracting BPM changes from beatmap data: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts BPM changes from allBeatmapDataItems
        /// </summary>
        private void TryExtractFromAllBeatmapDataItems(IReadonlyBeatmapData beatmapData, float baseBpm, List<(float time, float bpm)> rawBpmChanges)
        {
            var allItemsProp = beatmapData.GetType().GetProperty("allBeatmapDataItems");
            if (allItemsProp == null) return;

            var allItems = allItemsProp.GetValue(beatmapData) as System.Collections.IEnumerable;
            if (allItems == null) return;

            foreach (var item in allItems)
            {
                if (item == null) continue;

                var itemType = item.GetType();
                if (itemType.Name.Contains("BPMChange") || itemType.Name.Contains("BpmChange"))
                {
                    float time = GetFloatProperty(item, "time") ?? GetFloatProperty(item, "_time") ?? 0f;
                    float newBpm = GetFloatProperty(item, "bpm") ?? GetFloatProperty(item, "_bpm") ?? baseBpm;

                    rawBpmChanges.Add((time, newBpm));
                    Plugin.Log.Debug($"Found BPM change: time={time}, bpm={newBpm}");
                }
            }
        }

        /// <summary>
        /// Extracts BPM changes from GetBeatmapDataItems&lt;T&gt;
        /// </summary>
        private void TryExtractFromGetBeatmapDataItems(IReadonlyBeatmapData beatmapData, float baseBpm, List<(float time, float bpm)> rawBpmChanges)
        {
            var bpmChangeType = FindType("BPMChangeBeatmapEventData");
            if (bpmChangeType == null) return;

            var getItemsMethod = beatmapData.GetType().GetMethod("GetBeatmapDataItems");
            if (getItemsMethod == null) return;

            var genericMethod = getItemsMethod.MakeGenericMethod(bpmChangeType);
            var items = genericMethod.Invoke(beatmapData, null) as System.Collections.IEnumerable;
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null) continue;

                float time = GetFloatProperty(item, "time") ?? 0f;
                float newBpm = GetFloatProperty(item, "bpm") ?? baseBpm;

                rawBpmChanges.Add((time, newBpm));
                Plugin.Log.Debug($"Found BPM change via GetBeatmapDataItems: time={time}, bpm={newBpm}");
            }
        }

        /// <summary>
        /// Extracts BPM changes from beatmapEventsData (for older versions)
        /// </summary>
        private void TryExtractFromBeatmapEventsData(IReadonlyBeatmapData beatmapData, float baseBpm, List<(float time, float bpm)> rawBpmChanges)
        {
            var eventsDataProp = beatmapData.GetType().GetProperty("beatmapEventsData");
            if (eventsDataProp == null) return;

            var eventsData = eventsDataProp.GetValue(beatmapData) as System.Collections.IEnumerable;
            if (eventsData == null) return;

            foreach (var eventItem in eventsData)
            {
                if (eventItem == null) continue;

                var eventType = eventItem.GetType();
                if (eventType.Name.Contains("BPM"))
                {
                    float time = GetFloatProperty(eventItem, "time") ?? 0f;
                    float newBpm = GetFloatProperty(eventItem, "bpm") ?? GetFloatProperty(eventItem, "value") ?? baseBpm;

                    rawBpmChanges.Add((time, newBpm));
                    Plugin.Log.Debug($"Found BPM change via beatmapEventsData: time={time}, bpm={newBpm}");
                }
            }
        }

        /// <summary>
        /// Processes BPM change events with time unit determination.
        /// Beat Saber's BPMChangeBeatmapEventData.time is stored in seconds.
        /// </summary>
        private void ProcessBpmChanges(List<(float time, float bpm)> rawBpmChanges, float baseBpm, List<BpmChangeEvent> bpmChanges)
        {
            if (rawBpmChanges.Count == 0)
                return;

            // Sort by time
            rawBpmChanges.Sort((a, b) => a.time.CompareTo(b.time));

            foreach (var (time, newBpm) in rawBpmChanges)
            {
                // Beat Saber's time is already in seconds, use as is
                float timeInSeconds = time;

                // Negative time is before song start, treat as 0
                if (timeInSeconds < 0)
                {
                    Plugin.Log.Info($"BPM change at {timeInSeconds:F3}s (before song start): {newBpm} BPM - treating as 0s");
                    timeInSeconds = 0f;
                }

                bpmChanges.Add(new BpmChangeEvent(timeInSeconds, newBpm));
                Plugin.Log.Debug($"BPM change at {timeInSeconds:F3}s: {newBpm} BPM");
            }

            // Merge duplicate time events (keep last one)
            for (int i = bpmChanges.Count - 2; i >= 0; i--)
            {
                if (Math.Abs(bpmChanges[i].Time - bpmChanges[i + 1].Time) < 0.001f)
                {
                    bpmChanges.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Gets float property from an object
        /// </summary>
        private float? GetFloatProperty(object obj, string propertyName)
        {
            try
            {
                var type = obj.GetType();

                // Try property
                var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value is float f) return f;
                    if (value is double d) return (float)d;
                    if (value is int i) return i;
                }

                // Try field
                var field = type.GetField(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(obj);
                    if (value is float f) return f;
                    if (value is double d) return (float)d;
                    if (value is int i) return i;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Finds type by type name
        /// </summary>
        private Type? FindType(string typeName)
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var type = assembly.GetType(typeName);
                        if (type != null) return type;

                        // If not full name, search within assembly
                        foreach (var t in assembly.GetTypes())
                        {
                            if (t.Name == typeName)
                                return t;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }
    }
}
