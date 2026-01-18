using System;
using FaraRhythmMarker.Configuration;

namespace FaraRhythmMarker.Models
{
    /// <summary>
    /// Calculates beat timing based on BPM and division settings
    /// </summary>
    internal class BeatTimingModel
    {
        public float Bpm { get; private set; }
        public float Division { get; private set; }
        public float BeatInterval { get; private set; }

        private int _lastBeatIndex = -1;
        private float _startTimeOffset = 0f;

        public event Action<int, float>? OnBeat; // Passes color index and hit time

        public void Initialize(float bpm, float startTimeOffset = 0f)
        {
            Bpm = bpm;
            _startTimeOffset = startTimeOffset;
            Division = PluginConfig.Instance.BeatDivision;
            CalculateBeatInterval();

            Plugin.Log.Info($"BeatTimingModel: BPM={Bpm}, Division={Division}, Interval={BeatInterval:F4}s, Offset={_startTimeOffset}s");
            
            // 初期化時に、3ビート先までのマーカーを生成するために _lastBeatIndex を調整するか、
            // Update の最初の呼び出しでバックログを生成するようにする。
            // ここでは -1 のままにしておき、Update で 0 から生成させる。
        }

        public void SetStartTimeOffset(float offset)
        {
            _startTimeOffset = offset;
        }

        public void UpdateDivision(float division)
        {
            Division = division;
            CalculateBeatInterval();
        }

        private void CalculateBeatInterval()
        {
            // BeatDivision 0.25 = 1/4 beat, 0.5 = 1/2 beat, 1 = 1 beat, 2 = 2 beats
            BeatInterval = (60f / Bpm) * Division;
        }

        /// <summary>
        /// Updates the timing model with current song time
        /// </summary>
        /// <param name="songTime">Current time in the song (seconds)</param>
        public void Update(float songTime)
        {
            if (BeatInterval <= 0)
                return;

            // 4ビート先までマーカーを表示したいため、生成のタイミングを早める。
            // 現在の songTime に対して、どのビートまで生成済みかを管理する。
            
            // 4ビート分の時間を計算
            float lookAheadTime = (60f / Bpm) * 4f;
            
            // ターゲットとなる（生成すべき）ビートのインデックス
            // songTime + lookAheadTime の時点でのビートインデックスまで生成する
            // 基準時間を songTime - _startTimeOffset にずらす
            float adjustedTime = songTime - _startTimeOffset;
            int maxBeatIndexToSpawn = (int)((adjustedTime + lookAheadTime) / BeatInterval);

            while (_lastBeatIndex < maxBeatIndexToSpawn)
            {
                _lastBeatIndex++;
                
                // hitTime はそのビートがプレイヤーの足元に到達すべき時間
                float hitTime = (_lastBeatIndex * BeatInterval) + _startTimeOffset;

                // すでに過ぎ去ったビートは生成しない（曲の途中から始まった場合など）
                if (hitTime < songTime) continue;

                // Calculate color index
                int colorMode = PluginConfig.Instance.ColorMode;
                int colorIndex = _lastBeatIndex % colorMode;

                OnBeat?.Invoke(colorIndex, hitTime);
            }
        }

        public void Reset()
        {
            _lastBeatIndex = -1;
        }

        /// <summary>
        /// Gets the number of markers per minute for display purposes
        /// </summary>
        public int GetMarkersPerMinute()
        {
            return (int)(Bpm / Division);
        }
    }
}
