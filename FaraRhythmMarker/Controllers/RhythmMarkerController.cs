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
        private readonly BeatmapLevel _beatmapLevel;
        private readonly GameplayCoreSceneSetupData _sceneSetupData;
        private readonly PlayerTransforms _playerTransforms;
        private readonly IAudioTimeSource _audioTimeSource;

        private readonly BeatmapObjectSpawnController.InitData? _spawnInitData;
        private readonly BeatTimingModel _model;
        private readonly MarkerView _view;
        private bool _songStarted = false;
        private float _songStartTime = 0f;

        public RhythmMarkerController(
            AudioTimeSyncController audioTimeSyncController,
            BeatmapLevel beatmapLevel,
            GameplayCoreSceneSetupData sceneSetupData,
            PlayerTransforms playerTransforms,
            IAudioTimeSource audioTimeSource,
            [InjectOptional] BeatmapObjectSpawnController.InitData? spawnInitData = null)
        {
            _audioTimeSyncController = audioTimeSyncController;
            _beatmapLevel = beatmapLevel;
            _sceneSetupData = sceneSetupData;
            _playerTransforms = playerTransforms;
            _audioTimeSource = audioTimeSource;
            _spawnInitData = spawnInitData;

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
                // 1.40.8 では transformedBeatmapData や beatmapLevel.GetDifficultyBeatmapData から取得可能
                var difficultyData = _sceneSetupData.beatmapLevel.GetDifficultyBeatmapData(_sceneSetupData.beatmapKey.beatmapCharacteristic, _sceneSetupData.beatmapKey.difficulty);
                if (difficultyData != null)
                {
                    njs = difficultyData.noteJumpMovementSpeed;
                }
                else if (_spawnInitData != null)
                {
                    njs = _spawnInitData.noteJumpMovementSpeed;
                }

                if (_spawnInitData != null)
                {
                    Plugin.Log.Info($"SpawnInitData: noteJumpValue={_spawnInitData.noteJumpValue}, noteJumpValueType={_spawnInitData.noteJumpValueType}, NJS={_spawnInitData.noteJumpMovementSpeed}");
                }

                // BeatmapObjectSpawnCenter provides the actual Z offset for the spawn center
                // which is often where the notes are meant to be hit.
                var spawnCenter = GameObject.FindObjectOfType<BeatmapObjectSpawnCenter>();
                if (spawnCenter != null)
                {
                    // BeatSaber 1.40.8 では、通常この spawnCenter の位置がプレイヤーの足元の基準（Z=0付近）に相当する。
                    // ユーザーのフィードバックに基づき、ノーツを切る位置の感覚に合わせるため
                    // プレイヤー足場の前面付近（約 0.5m 奥）にヒット位置をオフセットする。
                    _view.HitZOffset = spawnCenter.transform.position.z + 0.5f; 
                    Plugin.Log.Info($"Found BeatmapObjectSpawnCenter at Z: {spawnCenter.transform.position.z}. Set HitZOffset to: {_view.HitZOffset} (with 0.5m offset)");
                }
                else
                {
                    // BeatmapObjectSpawnCenter が見つからない場合は、デフォルトのヒット位置 Z=0.5f を使用する
                    _view.HitZOffset = 0.5f;
                    Plugin.Log.Info($"BeatmapObjectSpawnCenter not found, using default HitZOffset: 0.5");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Failed to get NJS or spawn data: {ex.Message}. Using default 12.");
                _view.HitZOffset = 0f;
            }

            // Initialize Model
            float bpm = _beatmapLevel.beatsPerMinute;
            _model.Initialize(bpm);
            _model.OnBeat += OnBeatTriggered;

            // Initialize View
            _view.Initialize(_audioTimeSyncController, _sceneSetupData, _playerTransforms, njs);
            
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
    }
}
