using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace FaraRhythmMarker.Configuration
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; } = null!;

        /// <summary>
        /// Whether the rhythm marker is enabled
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>
        /// Beat division (0.25 = 1/4 beat, 0.5 = 1/2 beat, 1 = 1 beat, 2 = 2 beats)
        /// </summary>
        public virtual float BeatDivision { get; set; } = 1.0f;

        /// <summary>
        /// Color mode: 1 = Single color, 2 = Two colors, 4 = Four colors
        /// </summary>
        public virtual int ColorMode { get; set; } = 1;

        // Color 1 (Default: Purple)
        public virtual float Color1R { get; set; } = 0.5f;
        public virtual float Color1G { get; set; } = 0.0f;
        public virtual float Color1B { get; set; } = 0.5f;

        // Color 2
        public virtual float Color2R { get; set; } = 0.0f;
        public virtual float Color2G { get; set; } = 1.0f;
        public virtual float Color2B { get; set; } = 0.0f;

        // Color 3
        public virtual float Color3R { get; set; } = 0.0f;
        public virtual float Color3G { get; set; } = 0.0f;
        public virtual float Color3B { get; set; } = 1.0f;

        // Color 4
        public virtual float Color4R { get; set; } = 1.0f;
        public virtual float Color4G { get; set; } = 1.0f;
        public virtual float Color4B { get; set; } = 0.0f;

        /// <summary>
        /// Legacy marker color R (0-1) - kept for compatibility
        /// </summary>
        public virtual float MarkerColorR { get; set; } = 1.0f;

        /// <summary>
        /// Legacy marker color G (0-1) - kept for compatibility
        /// </summary>
        public virtual float MarkerColorG { get; set; } = 1.0f;

        /// <summary>
        /// Legacy marker color B (0-1) - kept for compatibility
        /// </summary>
        public virtual float MarkerColorB { get; set; } = 1.0f;

        /// <summary>
        /// Marker opacity (0-1)
        /// </summary>
        public virtual float MarkerOpacity { get; set; } = 0.8f;

        /// <summary>
        /// Marker size
        /// </summary>
        public virtual float MarkerSize { get; set; } = 50.0f;

        /// <summary>
        /// Marker flash duration in seconds
        /// </summary>
        public virtual float FlashDuration { get; set; } = 0.05f;

        /// <summary>
        /// マーカーのZ座標オフセット（0-2、デフォルト1）
        /// プレイヤーの音声遅延に合わせて調整可能
        /// </summary>
        public virtual float MarkerZOffset { get; set; } = 1.0f;

        /// <summary>
        /// Z座標オフセットの変更ステップ（0.1, 0.05, 0.01）
        /// </summary>
        public virtual float MarkerZOffsetStep { get; set; } = 0.1f;

        /// <summary>
        /// Called when config changes
        /// </summary>
        public virtual void Changed()
        {
            // Auto-save handled by IPA
        }

        /// <summary>
        /// Called when config is reloaded
        /// </summary>
        public virtual void OnReload()
        {
            // Called when the config is reloaded from disk
            Plugin.Log?.Info($"Config reloaded: Enabled={Enabled}, ColorMode={ColorMode}");
        }
    }
}
