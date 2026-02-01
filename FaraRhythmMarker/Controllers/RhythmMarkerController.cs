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

            // 設定からのZ座標オフセットを適用
            float configZOffset = PluginConfig.Instance.MarkerZOffset;
            _view.HitZOffset += configZOffset;
            Plugin.Log.Info($"Applied config MarkerZOffset: {configZOffset}. Final HitZOffset: {_view.HitZOffset}");

            // Initialize Model
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
        /// NJSとヒット位置を初期化する
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

                bool foundJumpEndPos = TryGetJumpEndPos();

                if (!foundJumpEndPos)
                {
                    ApplyFallbackHitPosition();
                    TryGetPlatformBounds();
                }
                else
                {
                    TryGetPlatformXOffset();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get NJS or spawn data: {ex.Message}. Using default 12.");
                _view.HitZOffset = 0f;
            }

            return njs;
        }

        /// <summary>
        /// BeatmapObjectSpawnControllerからjumpEndPosを取得する
        /// </summary>
        private bool TryGetJumpEndPos()
        {
            var spawnController = _spawnController ?? GameObject.FindObjectOfType<BeatmapObjectSpawnController>();
            if (spawnController == null)
            {
                Plugin.Log.Warn("BeatmapObjectSpawnController not found");
                return false;
            }

            Plugin.Log.Info($"Found BeatmapObjectSpawnController: {spawnController.name}");

            try
            {
                var movementData = GetMovementData(spawnController);
                if (movementData == null)
                {
                    LogAvailableFields(spawnController.GetType());
                    return false;
                }

                var jumpEndPosValue = GetJumpEndPosFromMovementData(movementData);
                if (jumpEndPosValue is Vector3 jumpEndPos)
                {
                    _view.HitZOffset = jumpEndPos.z;
                    Plugin.Log.Info($"Got jumpEndPos: {jumpEndPos}. Set HitZOffset to: {_view.HitZOffset}");
                    return true;
                }

                LogAvailableMembers(movementData.GetType());
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get jumpEndPos via reflection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// SpawnControllerからMovementDataを取得する
        /// </summary>
        private object? GetMovementData(BeatmapObjectSpawnController spawnController)
        {
            var spawnControllerType = spawnController.GetType();
            string[] fieldNames = { "_beatmapObjectSpawnMovementData", "_spawnMovementData", "beatmapObjectSpawnMovementData" };

            // フィールドから取得を試みる
            foreach (var fieldName in fieldNames)
            {
                var field = spawnControllerType.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(spawnController);
                    if (value != null)
                    {
                        Plugin.Log.Info($"Found movement data via field: {fieldName}");
                        return value;
                    }
                }
            }

            // プロパティから取得を試みる
            var props = spawnControllerType.GetProperties(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.Name.ToLower().Contains("movementdata") || prop.Name.ToLower().Contains("spawn"))
                {
                    try
                    {
                        var value = prop.GetValue(spawnController);
                        if (value != null)
                        {
                            Plugin.Log.Info($"Found movement data via property: {prop.Name}");
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Debug($"Failed to get property {prop.Name}: {ex.Message}");
                    }
                }
            }

            Plugin.Log.Warn("Movement data not found in BeatmapObjectSpawnController");
            return null;
        }

        /// <summary>
        /// MovementDataからjumpEndPosを取得する
        /// </summary>
        private object? GetJumpEndPosFromMovementData(object movementData)
        {
            var movementDataType = movementData.GetType();
            Plugin.Log.Info($"Movement data type: {movementDataType.FullName}");

            string[] propertyNames = { "jumpEndPos", "_jumpEndPos" };
            var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            foreach (var name in propertyNames)
            {
                // プロパティを試す
                var prop = movementDataType.GetProperty(name, bindingFlags);
                if (prop != null)
                {
                    var value = prop.GetValue(movementData);
                    if (value != null)
                    {
                        Plugin.Log.Info($"Found {name} as property");
                        return value;
                    }
                }

                // フィールドを試す
                var field = movementDataType.GetField(name, bindingFlags);
                if (field != null)
                {
                    var value = field.GetValue(movementData);
                    if (value != null)
                    {
                        Plugin.Log.Info($"Found {name} as field");
                        return value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// フォールバックのヒット位置を適用する
        /// </summary>
        private void ApplyFallbackHitPosition()
        {
            var spawnCenter = GameObject.FindObjectOfType<BeatmapObjectSpawnCenter>();
            if (spawnCenter != null)
            {
                _view.HitZOffset = spawnCenter.transform.position.z;
                Plugin.Log.Info($"Fallback: Found BeatmapObjectSpawnCenter at Z: {spawnCenter.transform.position.z}");
            }
            else
            {
                _view.HitZOffset = 0f;
                Plugin.Log.Info($"Fallback: Using default HitZOffset: 0");
            }
        }

        /// <summary>
        /// デバッグ用: 利用可能なフィールドをログに出力
        /// </summary>
        private void LogAvailableFields(Type type)
        {
            Plugin.Log.Info($"Available fields in {type.Name}:");
            foreach (var field in type.GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                Plugin.Log.Info($"  - Field: {field.Name} ({field.FieldType.Name})");
            }
        }

        /// <summary>
        /// デバッグ用: 利用可能なメンバーをログに出力
        /// </summary>
        private void LogAvailableMembers(Type type)
        {
            Plugin.Log.Info($"jumpEndPos not found. Available members in {type.Name}:");
            foreach (var member in type.GetMembers(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if (member.Name.ToLower().Contains("jump") || member.Name.ToLower().Contains("pos") || member.Name.ToLower().Contains("end"))
                {
                    Plugin.Log.Info($"  - {member.MemberType}: {member.Name}");
                }
            }
        }

        private void OnBeatTriggered(int colorIndex, float hitTime)
        {
            _view.SpawnMarker(colorIndex, hitTime);
        }

        public void Tick()
        {
            if (!PluginConfig.Instance.Enabled || !_view.IsInitialized)
                return;

            // Update Model with current song time
            float songTime = _audioTimeSyncController.songTime;

            // 曲の開始を検知する
            if (!_songStarted && songTime > 0)
            {
                _songStarted = true;
                // 曲開始を 0 ビート目とするため、現在の songTime をオフセットとして設定
                _songStartTime = songTime;
                _model.SetStartTimeOffset(_songStartTime);
                Plugin.Log.Info($"Song start detected at songTime: {_songStartTime}. This is now beat 0.");
            }

            // 曲開始後のみビートの計算を行う
            if (_songStarted)
            {
                _model.Update(songTime);
            }

            // マーカーの移動およびサイドライトの更新 (サイドライトは曲開始前でも更新する)
            _view.UpdateMarkers();
        }

        public void Dispose()
        {
            Plugin.Log.Info("RhythmMarkerController.Dispose called");
            _model.OnBeat -= OnBeatTriggered;
            _view.Dispose();
        }

        /// <summary>
        /// 足場からX幅のみ取得する（jumpEndPosが取得できた場合用）
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
        /// 足場のGameObjectを探す
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

            // 名前で見つからない場合、検索で探す
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
        /// 足場（PlayersPlace）のGameObjectを探して、その境界からマーカー位置を設定する
        /// </summary>
        private void TryGetPlatformBounds()
        {
            try
            {
                var platformObj = FindPlatformGameObject();

                if (platformObj != null)
                {
                    Plugin.Log.Info($"Found platform GameObject: {platformObj.name}");

                    // Rendererから境界を取得
                    var renderer = platformObj.GetComponent<Renderer>() ?? platformObj.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        var bounds = renderer.bounds;

                        // 足場の奥側の縁のZ座標（max.z）を取得
                        float platformFrontZ = bounds.max.z;
                        _view.HitZOffset = platformFrontZ;

                        // 足場の左右の幅（X方向の半分）を取得
                        float platformHalfWidth = bounds.extents.x;
                        _view.PlatformXOffset = platformHalfWidth;

                        Plugin.Log.Info($"Platform bounds - Center: {bounds.center}, Size: {bounds.size}");
                        Plugin.Log.Info($"Set HitZOffset to platform front edge: {platformFrontZ}");
                        Plugin.Log.Info($"Set PlatformXOffset to platform half-width: {platformHalfWidth}");
                    }
                    else
                    {
                        // Rendererがない場合、Transformの位置を使用
                        Plugin.Log.Warn($"Platform '{platformObj.name}' has no Renderer, using transform position");
                        _view.HitZOffset = platformObj.transform.position.z;
                    }
                }
                else
                {
                    Plugin.Log.Info("Platform GameObject not found, using default values");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get platform bounds: {ex.Message}");
            }
        }

        /// <summary>
        /// ビートマップデータからBPM変更イベントを抽出する
        /// </summary>
        /// <param name="baseBpm">基本BPM</param>
        /// <returns>BPM変更イベントのリスト</returns>
        private List<BpmChangeEvent> ExtractBpmChanges(float baseBpm)
        {
            var bpmChanges = new List<BpmChangeEvent>();

            try
            {
                // まず注入されたbeatmapDataを試す
                IReadonlyBeatmapData? beatmapData = _beatmapData;

#if BS_1_29_1
                // 1.29.1では IDifficultyBeatmap から取得
                if (beatmapData == null && _difficultyBeatmap != null)
                {
                    try
                    {
                        // beatmapDataプロパティにアクセス
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

                // BPM変更イベントを探す（リフレクションを使用して互換性を確保）
                ExtractBpmChangesFromBeatmapData(beatmapData, baseBpm, bpmChanges);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to extract BPM changes: {ex.Message}");
            }

            return bpmChanges;
        }

        /// <summary>
        /// ビートマップデータからBPM変更イベントを抽出（リフレクション使用）
        /// </summary>
        private void ExtractBpmChangesFromBeatmapData(IReadonlyBeatmapData beatmapData, float baseBpm, List<BpmChangeEvent> bpmChanges)
        {
            var rawBpmChanges = new List<(float time, float bpm)>();

            try
            {
                // 複数の方法でBPM変更を取得
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
        /// allBeatmapDataItemsからBPM変更を抽出
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
                    Plugin.Log.Info($"Found BPM change: time={time}, bpm={newBpm}");
                }
            }
        }

        /// <summary>
        /// GetBeatmapDataItems<T>からBPM変更を抽出
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
                Plugin.Log.Info($"Found BPM change via GetBeatmapDataItems: time={time}, bpm={newBpm}");
            }
        }

        /// <summary>
        /// beatmapEventsDataからBPM変更を抽出（古いバージョン用）
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
                    Plugin.Log.Info($"Found BPM change via beatmapEventsData: time={time}, bpm={newBpm}");
                }
            }
        }

        /// <summary>
        /// BPM変更イベントの時間単位を判定して処理する
        /// Beat Saberの BPMChangeBeatmapEventData.time は秒単位で格納されている
        /// </summary>
        private void ProcessBpmChanges(List<(float time, float bpm)> rawBpmChanges, float baseBpm, List<BpmChangeEvent> bpmChanges)
        {
            if (rawBpmChanges.Count == 0)
                return;

            // 時間順にソート
            rawBpmChanges.Sort((a, b) => a.time.CompareTo(b.time));

            foreach (var (time, newBpm) in rawBpmChanges)
            {
                // Beat Saberの BPMChangeBeatmapEventData.time は既に秒単位
                // そのまま使用する
                float timeInSeconds = time;

                // 負の時間は曲開始前なのでスキップするか、0として扱う
                if (timeInSeconds < 0)
                {
                    Plugin.Log.Info($"BPM change at {timeInSeconds:F3}s (before song start): {newBpm} BPM - treating as 0s");
                    timeInSeconds = 0f;
                }

                bpmChanges.Add(new BpmChangeEvent(timeInSeconds, newBpm));
                Plugin.Log.Info($"BPM change at {timeInSeconds:F3}s: {newBpm} BPM");
            }

            // 重複する時間のイベントを統合（最後のものを使用）
            for (int i = bpmChanges.Count - 2; i >= 0; i--)
            {
                if (Math.Abs(bpmChanges[i].Time - bpmChanges[i + 1].Time) < 0.001f)
                {
                    bpmChanges.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// オブジェクトからfloatプロパティを取得
        /// </summary>
        private float? GetFloatProperty(object obj, string propertyName)
        {
            try
            {
                var type = obj.GetType();

                // プロパティを試す
                var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value is float f) return f;
                    if (value is double d) return (float)d;
                    if (value is int i) return i;
                }

                // フィールドを試す
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
        /// 型名から型を検索
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

                        // フルネームでなければ、アセンブリ内を検索
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
