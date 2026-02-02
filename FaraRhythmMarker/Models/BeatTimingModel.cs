using System;
using System.Collections.Generic;
using FaraRhythmMarker.Configuration;

namespace FaraRhythmMarker.Models
{
    /// <summary>
    /// BPM変更イベントを表す構造体
    /// </summary>
    internal struct BpmChangeEvent
    {
        public float Time;  // 変更が発生する時間（秒）
        public float Bpm;   // 新しいBPM

        public BpmChangeEvent(float time, float bpm)
        {
            Time = time;
            Bpm = bpm;
        }
    }

    /// <summary>
    /// Calculates beat timing based on BPM and division settings
    /// Supports mid-song BPM changes
    /// </summary>
    internal class BeatTimingModel
    {
        public float BaseBpm { get; private set; }
        public float Division { get; private set; }

        private readonly List<BpmChangeEvent> _bpmChanges = new List<BpmChangeEvent>();
        private readonly List<float> _beatTimes = new List<float>(); // 各ビートの発生時刻をキャッシュ
        private int _lastSpawnedBeatIndex = -1;
        private float _startTimeOffset = 0f;
        private bool _beatsPrecomputed = false;
        private float _songDuration = 300f; // デフォルト5分

        public event Action<int, float>? OnBeat; // Passes color index and hit time

        public void Initialize(float baseBpm, float startTimeOffset = 0f)
        {
            BaseBpm = baseBpm;
            _startTimeOffset = startTimeOffset;
            Division = PluginConfig.Instance.BeatDivision;

            // BPM変更がない場合、ベースBPMのみで初期化
            if (_bpmChanges.Count == 0)
            {
                _bpmChanges.Add(new BpmChangeEvent(0f, baseBpm));
            }

            Plugin.Log.Info($"BeatTimingModel: BaseBPM={BaseBpm}, Division={Division}, BpmChanges={_bpmChanges.Count}, Offset={_startTimeOffset}s");
        }

        /// <summary>
        /// BPM変更イベントを設定
        /// </summary>
        /// <param name="bpmChanges">時間順にソートされたBPM変更イベントのリスト</param>
        /// <param name="songDuration">曲の長さ（秒）</param>
        public void SetBpmChanges(List<BpmChangeEvent> bpmChanges, float songDuration = 300f)
        {
            _bpmChanges.Clear();
            _songDuration = songDuration;

            if (bpmChanges == null || bpmChanges.Count == 0)
            {
                // BPM変更がない場合はベースBPMを使用
                _bpmChanges.Add(new BpmChangeEvent(0f, BaseBpm));
            }
            else
            {
                // 時間順にソート
                bpmChanges.Sort((a, b) => a.Time.CompareTo(b.Time));

                // 最初のイベントが0秒でない場合、ベースBPMを追加
                if (bpmChanges[0].Time > 0.001f)
                {
                    _bpmChanges.Add(new BpmChangeEvent(0f, BaseBpm));
                }

                _bpmChanges.AddRange(bpmChanges);
            }

            _beatsPrecomputed = false;
            Plugin.Log.Info($"BeatTimingModel: Set {_bpmChanges.Count} BPM change events, song duration: {_songDuration}s");

            foreach (var change in _bpmChanges)
            {
                Plugin.Log.Info($"  BPM Change at {change.Time:F2}s: {change.Bpm} BPM");
            }
        }

        public void SetStartTimeOffset(float offset)
        {
            _startTimeOffset = offset;
            _beatsPrecomputed = false; // 再計算が必要
        }

        public void UpdateDivision(float division)
        {
            Division = division;
            _beatsPrecomputed = false; // 再計算が必要
        }

        /// <summary>
        /// 指定時刻でのBPMを取得（公開版）
        /// </summary>
        public float GetBpmAtTime(float time)
        {
            float currentBpm = BaseBpm;

            for (int i = _bpmChanges.Count - 1; i >= 0; i--)
            {
                if (time >= _bpmChanges[i].Time)
                {
                    currentBpm = _bpmChanges[i].Bpm;
                    break;
                }
            }

            return currentBpm;
        }

        /// <summary>
        /// 全ビートの発生時刻を事前計算
        /// BPM変更がある場合、各区間ごとに計算する必要がある
        /// </summary>
        private void PrecomputeBeatTimes()
        {
            if (_beatsPrecomputed) return;

            _beatTimes.Clear();

            float currentTime = 0f;
            int bpmChangeIndex = 0;
            float currentBpm = _bpmChanges.Count > 0 ? _bpmChanges[0].Bpm : BaseBpm;

            // 曲の長さ + 余裕分まで計算
            float maxTime = _songDuration + 10f;

            while (currentTime < maxTime)
            {
                // 現在のBPMでのビート間隔
                float beatInterval = (60f / currentBpm) * Division;

                // 次のBPM変更までの時間を確認
                float nextBpmChangeTime = maxTime;
                if (bpmChangeIndex + 1 < _bpmChanges.Count)
                {
                    nextBpmChangeTime = _bpmChanges[bpmChangeIndex + 1].Time;
                }

                // 現在のBPM区間でビートを生成
                while (currentTime < nextBpmChangeTime && currentTime < maxTime)
                {
                    _beatTimes.Add(currentTime);
                    currentTime += beatInterval;
                }

                // 次のBPM区間へ
                if (bpmChangeIndex + 1 < _bpmChanges.Count)
                {
                    bpmChangeIndex++;
                    currentBpm = _bpmChanges[bpmChangeIndex].Bpm;
                    // BPM変更ポイントに時刻を合わせる（ギャップを防ぐ）
                    if (currentTime < nextBpmChangeTime)
                    {
                        currentTime = nextBpmChangeTime;
                    }
                }
            }

            _beatsPrecomputed = true;
            Plugin.Log.Info($"BeatTimingModel: Precomputed {_beatTimes.Count} beat times");
        }

        /// <summary>
        /// Updates the timing model with current song time
        /// </summary>
        /// <param name="songTime">Current time in the song (seconds)</param>
        public void Update(float songTime)
        {
            // ビート時刻が未計算なら計算
            if (!_beatsPrecomputed)
            {
                PrecomputeBeatTimes();
            }

            if (_beatTimes.Count == 0)
                return;

            // 4ビート先までマーカーを表示
            float lookAheadTime = 4f * (60f / GetBpmAtTime(songTime));

            // 調整後の時刻
            float adjustedTime = songTime - _startTimeOffset;
            float targetTime = adjustedTime + lookAheadTime;

            // まだ生成していないビートを生成
            while (_lastSpawnedBeatIndex + 1 < _beatTimes.Count)
            {
                int nextIndex = _lastSpawnedBeatIndex + 1;
                float beatTime = _beatTimes[nextIndex];

                // このビートのhitTimeを計算（オフセット適用）
                float hitTime = beatTime + _startTimeOffset;

                // まだ先のビートなら終了
                if (beatTime > targetTime)
                    break;

                _lastSpawnedBeatIndex = nextIndex;

                // すでに過ぎ去ったビートは生成しない
                if (hitTime < songTime)
                    continue;

                // Calculate color index
                int colorMode = PluginConfig.Instance.ColorMode;
                int colorIndex = nextIndex % colorMode;

                OnBeat?.Invoke(colorIndex, hitTime);
            }
        }

        public void Reset()
        {
            _lastSpawnedBeatIndex = -1;
        }

        /// <summary>
        /// Gets the current BPM for display purposes
        /// </summary>
        public float GetCurrentBpm(float songTime)
        {
            return GetBpmAtTime(songTime - _startTimeOffset);
        }

        /// <summary>
        /// Gets the number of markers per minute at base BPM for display purposes
        /// </summary>
        public int GetMarkersPerMinute()
        {
            return (int)(BaseBpm / Division);
        }
    }
}
