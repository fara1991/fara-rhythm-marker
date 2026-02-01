using System;
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
                // Make sure view is disposed if it was previously initialized
                if (_view.IsInitialized)
                {
                    _view.Dispose();
                }
                return;
            }

            // Get NJS from sceneSetupData
            float njs = 12f;
            try
            {
                // NoteJumpMovementSpeed (NJS) の取得
#if BS_1_29_1
                njs = _difficultyBeatmap.noteJumpMovementSpeed;
#else
                njs = 12f;
                // In 1.34+, NJS is often in BeatmapObjectSpawnController.InitData
                if (_spawnInitData != null)
                {
                    njs = _spawnInitData.noteJumpMovementSpeed;
                }
#endif

                if (_spawnInitData != null)
                {
                    Plugin.Log.Info($"SpawnInitData: noteJumpValue={_spawnInitData.noteJumpValue}, noteJumpValueType={_spawnInitData.noteJumpValueType}, NJS={_spawnInitData.noteJumpMovementSpeed}");
                }

                // BeatmapObjectSpawnController から jumpEndPos（ノーツのカット位置）を取得
                bool foundJumpEndPos = false;

                // まず注入されたコントローラーを試す、なければFindObjectOfTypeで探す
                var spawnController = _spawnController ?? GameObject.FindObjectOfType<BeatmapObjectSpawnController>();

                if (spawnController != null)
                {
                    Plugin.Log.Info($"Found BeatmapObjectSpawnController: {spawnController.name}");
                    try
                    {
                        // リフレクションを使用して _beatmapObjectSpawnMovementData フィールドにアクセス
                        var spawnControllerType = spawnController.GetType();

                        // 複数のフィールド名パターンを試す
                        string[] fieldNames = { "_beatmapObjectSpawnMovementData", "_spawnMovementData", "beatmapObjectSpawnMovementData" };
                        object? movementData = null;

                        foreach (var fieldName in fieldNames)
                        {
                            var field = spawnControllerType.GetField(fieldName,
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (field != null)
                            {
                                movementData = field.GetValue(spawnController);
                                if (movementData != null)
                                {
                                    Plugin.Log.Info($"Found movement data via field: {fieldName}");
                                    break;
                                }
                            }
                        }

                        // プロパティも試す
                        if (movementData == null)
                        {
                            var props = spawnControllerType.GetProperties(
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            foreach (var prop in props)
                            {
                                if (prop.Name.ToLower().Contains("movementdata") || prop.Name.ToLower().Contains("spawn"))
                                {
                                    try
                                    {
                                        movementData = prop.GetValue(spawnController);
                                        if (movementData != null)
                                        {
                                            Plugin.Log.Info($"Found movement data via property: {prop.Name}");
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (movementData != null)
                        {
                            var movementDataType = movementData.GetType();
                            Plugin.Log.Info($"Movement data type: {movementDataType.FullName}");

                            // jumpEndPos を探す（プロパティとフィールド両方）
                            object? jumpEndPosValue = null;

                            var jumpEndPosProperty = movementDataType.GetProperty("jumpEndPos",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (jumpEndPosProperty != null)
                            {
                                jumpEndPosValue = jumpEndPosProperty.GetValue(movementData);
                                Plugin.Log.Info($"Found jumpEndPos as property");
                            }

                            if (jumpEndPosValue == null)
                            {
                                var jumpEndPosField = movementDataType.GetField("jumpEndPos",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (jumpEndPosField != null)
                                {
                                    jumpEndPosValue = jumpEndPosField.GetValue(movementData);
                                    Plugin.Log.Info($"Found jumpEndPos as field");
                                }
                            }

                            // _jumpEndPos も試す
                            if (jumpEndPosValue == null)
                            {
                                var jumpEndPosField = movementDataType.GetField("_jumpEndPos",
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (jumpEndPosField != null)
                                {
                                    jumpEndPosValue = jumpEndPosField.GetValue(movementData);
                                    Plugin.Log.Info($"Found _jumpEndPos as field");
                                }
                            }

                            if (jumpEndPosValue is Vector3 jumpEndPos)
                            {
                                _view.HitZOffset = jumpEndPos.z;
                                foundJumpEndPos = true;
                                Plugin.Log.Info($"Got jumpEndPos: {jumpEndPos}. Set HitZOffset to: {_view.HitZOffset}");
                            }
                            else
                            {
                                // 利用可能なメンバーをログに出力（デバッグ用）
                                Plugin.Log.Info($"jumpEndPos not found. Available members in {movementDataType.Name}:");
                                foreach (var member in movementDataType.GetMembers(
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                                {
                                    if (member.Name.ToLower().Contains("jump") || member.Name.ToLower().Contains("pos") || member.Name.ToLower().Contains("end"))
                                    {
                                        Plugin.Log.Info($"  - {member.MemberType}: {member.Name}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Plugin.Log.Warn("Movement data not found in BeatmapObjectSpawnController");
                            // 利用可能なフィールドをログに出力
                            Plugin.Log.Info($"Available fields in {spawnControllerType.Name}:");
                            foreach (var field in spawnControllerType.GetFields(
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                            {
                                Plugin.Log.Info($"  - Field: {field.Name} ({field.FieldType.Name})");
                            }
                        }
                    }
                    catch (Exception reflectionEx)
                    {
                        Plugin.Log.Warn($"Failed to get jumpEndPos via reflection: {reflectionEx.Message}");
                    }
                }
                else
                {
                    Plugin.Log.Warn("BeatmapObjectSpawnController not found (neither injected nor via FindObjectOfType)");
                }

                // フォールバック: BeatmapObjectSpawnCenter を使用
                if (!foundJumpEndPos)
                {
                    var spawnCenter = GameObject.FindObjectOfType<BeatmapObjectSpawnCenter>();
                    if (spawnCenter != null)
                    {
                        // spawnCenter の位置をそのまま使用（オフセットなし）
                        _view.HitZOffset = spawnCenter.transform.position.z;
                        Plugin.Log.Info($"Fallback: Found BeatmapObjectSpawnCenter at Z: {spawnCenter.transform.position.z}. Set HitZOffset to: {_view.HitZOffset}");
                    }
                    else
                    {
                        // デフォルトのヒット位置
                        _view.HitZOffset = 0f;
                        Plugin.Log.Info($"Fallback: BeatmapObjectSpawnCenter not found, using default HitZOffset: 0");
                    }

                    // jumpEndPos が取得できなかった場合のみ、足場の座標を使用
                    TryGetPlatformBounds();
                }
                else
                {
                    // jumpEndPos が取得できた場合は、足場からはX幅のみ取得
                    TryGetPlatformXOffset();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get NJS or spawn data: {ex.Message}. Using default 12.");
                _view.HitZOffset = 0f;
            }

            // 設定からのZ座標オフセットを適用
            float configZOffset = PluginConfig.Instance.MarkerZOffset;
            _view.HitZOffset += configZOffset;
            Plugin.Log.Info($"Applied config MarkerZOffset: {configZOffset}. Final HitZOffset: {_view.HitZOffset}");

            // Initialize Model
#if BS_1_29_1
            float bpm = _difficultyBeatmap.level.beatsPerMinute;
#else
            float bpm = _beatmapLevel?.beatsPerMinute ?? 120f;
#endif
            _model.Initialize(bpm);
            _model.OnBeat += OnBeatTriggered;

            // Initialize View
            _view.Initialize(_audioTimeSyncController, _playerTransforms, njs);
            
            Plugin.Log.Info($"RhythmMarkerController initialized: {_model.GetMarkersPerMinute()} markers/min, HitZOffset: {_view.HitZOffset}, NJS: {njs}");
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
    }
}
