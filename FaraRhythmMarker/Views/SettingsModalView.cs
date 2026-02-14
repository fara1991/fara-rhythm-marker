using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using TMPro;
using UnityEngine;

namespace FaraRhythmMarker.Views
{
    [ViewDefinition("FaraRhythmMarker.Views.SettingsView.bsml")]
#if BS_1_29_1
    [HotReload(RelativePathToLayout = @"..\Views\SettingsView.1.29.1.bsml")]
#else
    [HotReload(RelativePathToLayout = @"..\Views\SettingsView.1.40.8.bsml")]
#endif
    internal class SettingsModalView : BSMLAutomaticViewController
    {
        private SettingsMenuManager? _manager;

        public void Setup(SettingsMenuManager manager)
        {
            _manager = manager;
        }

        private bool IsJapanese => (_manager?.Language ?? "English") == "Japanese";
        private string L(string en, string ja) => IsJapanese ? ja : en;

        #region Localized Text via UIComponent

#pragma warning disable CS0649 // assigned by BSML via reflection
        [UIComponent("common-header-text")]
        private TextMeshProUGUI? _commonHeaderText;

        [UIComponent("color-header-text")]
        private TextMeshProUGUI? _colorHeaderText;

        [UIComponent("marker-guide-header-text")]
        private TextMeshProUGUI? _markerGuideHeaderText;

        [UIComponent("beat-division-warning-text")]
        private TextMeshProUGUI? _beatDivisionWarningText;
#pragma warning restore CS0649

        [UIValue("language-hint")]
        public string LanguageHint { get => L("Select display language", "設定画面の表示言語を選択します"); set { } }

        [UIValue("enabled-text")]
        public string EnabledText { get => L("Enabled", "有効"); set { } }
        [UIValue("enabled-hint")]
        public string EnabledHint { get => L("Enable/disable rhythm marker", "MOD全体の有効/無効を切り替えます"); set { } }

        [UIValue("color-mode-text")]
        public string ColorModeText { get => L("Color Mode", "カラーモード"); set { } }
        [UIValue("color-mode-hint")]
        public string ColorModeHint { get => L("Number of colors to cycle", "使用するカラーの数"); set { } }

        [UIValue("color1-text")]
        public string Color1Text { get => L("Color 1", "カラー 1"); set { } }
        [UIValue("color1-hint")]
        public string Color1Hint { get => L("First color", "1番目のカラー"); set { } }

        [UIValue("color2-text")]
        public string Color2Text { get => L("Color 2", "カラー 2"); set { } }
        [UIValue("color2-hint")]
        public string Color2Hint { get => L("Second color (2/4 color modes)", "2番目のカラー（2色・4色モード）"); set { } }

        [UIValue("color3-text")]
        public string Color3Text { get => L("Color 3", "カラー 3"); set { } }
        [UIValue("color3-hint")]
        public string Color3Hint { get => L("Third color (4 color mode)", "3番目のカラー（4色モード）"); set { } }

        [UIValue("color4-text")]
        public string Color4Text { get => L("Color 4", "カラー 4"); set { } }
        [UIValue("color4-hint")]
        public string Color4Hint { get => L("Fourth color (4 color mode)", "4番目のカラー（4色モード）"); set { } }

        [UIValue("beat-division-text")]
        public string BeatDivisionText { get => L("Beat Division", "ビート分割"); set { } }
        [UIValue("beat-division-hint")]
        public string BeatDivisionHint { get => L("Markers per beat", "1ビートあたりのマーカー数"); set { } }

        [UIValue("marker-opacity-text")]
        public string MarkerOpacityText { get => L("Marker Opacity", "マーカー透明度"); set { } }
        [UIValue("marker-opacity-hint")]
        public string MarkerOpacityHint { get => L("Marker transparency", "マーカーの透明度"); set { } }

        [UIValue("marker-size-text")]
        public string MarkerSizeText { get => L("Marker Size", "マーカーサイズ"); set { } }
        [UIValue("marker-size-hint")]
        public string MarkerSizeHint { get => L("Marker size in pixels", "マーカーのサイズ"); set { } }

        [UIValue("flash-duration-text")]
        public string FlashDurationText { get => L("Flash Duration", "フラッシュ時間"); set { } }
        [UIValue("flash-duration-hint")]
        public string FlashDurationHint { get => L("Flash effect duration (seconds)", "フラッシュの表示時間（秒）"); set { } }

        [UIValue("marker-z-offset-text")]
        public string MarkerZOffsetText { get => L("Marker Z Offset", "マーカーZ位置"); set { } }
        [UIValue("marker-z-offset-hint")]
        public string MarkerZOffsetHint { get => L("Z position offset for markers", "マーカーのZ座標オフセット"); set { } }

        [UIValue("z-offset-step-text")]
        public string ZOffsetStepText { get => L("Z Offset Step", "Zオフセット刻み幅"); set { } }
        [UIValue("z-offset-step-hint")]
        public string ZOffsetStepHint { get => L("Step size for Z offset slider", "Zオフセットスライダーの刻み幅"); set { } }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            UpdateHeaderTexts();
        }

        private void UpdateHeaderTexts()
        {
            if (_commonHeaderText != null)
                _commonHeaderText.text = L("Common", "共通");
            if (_colorHeaderText != null)
                _colorHeaderText.text = L("Color", "カラー");
            if (_markerGuideHeaderText != null)
                _markerGuideHeaderText.text = L("Marker Guide", "マーカーガイド");
            if (_beatDivisionWarningText != null)
                _beatDivisionWarningText.text = L(
                    "Using multiple colors is recommended for better visibility when 1/4 or 1/2 beat is selected.",
                    "1/4ビートまたは1/2ビートの場合、視認性を高めるために複数色の使用を推奨します。");
        }

        private void NotifyAllTextChanged()
        {
            UpdateHeaderTexts();
            NotifyPropertyChanged(nameof(LanguageHint));
            NotifyPropertyChanged(nameof(EnabledText));
            NotifyPropertyChanged(nameof(EnabledHint));
            NotifyPropertyChanged(nameof(ColorModeText));
            NotifyPropertyChanged(nameof(ColorModeHint));
            NotifyPropertyChanged(nameof(Color1Text));
            NotifyPropertyChanged(nameof(Color1Hint));
            NotifyPropertyChanged(nameof(Color2Text));
            NotifyPropertyChanged(nameof(Color2Hint));
            NotifyPropertyChanged(nameof(Color3Text));
            NotifyPropertyChanged(nameof(Color3Hint));
            NotifyPropertyChanged(nameof(Color4Text));
            NotifyPropertyChanged(nameof(Color4Hint));
            NotifyPropertyChanged(nameof(BeatDivisionText));
            NotifyPropertyChanged(nameof(BeatDivisionHint));
            NotifyPropertyChanged(nameof(MarkerOpacityText));
            NotifyPropertyChanged(nameof(MarkerOpacityHint));
            NotifyPropertyChanged(nameof(MarkerSizeText));
            NotifyPropertyChanged(nameof(MarkerSizeHint));
            NotifyPropertyChanged(nameof(FlashDurationText));
            NotifyPropertyChanged(nameof(FlashDurationHint));
            NotifyPropertyChanged(nameof(MarkerZOffsetText));
            NotifyPropertyChanged(nameof(MarkerZOffsetHint));
            NotifyPropertyChanged(nameof(ZOffsetStepText));
            NotifyPropertyChanged(nameof(ZOffsetStepHint));
        }

        #endregion

        #region UI Values

        [UIValue("language-options")]
        public List<object> LanguageOptions => _manager?.LanguageOptions ?? new List<object> { "English", "Japanese" };

        [UIValue("language")]
        public string Language
        {
            get => _manager?.Language ?? "English";
            set
            {
                if (_manager != null)
                {
                    _manager.Language = value;
                    NotifyAllTextChanged();
                }
            }
        }

        [UIValue("enabled")]
        public bool Enabled
        {
            get => _manager?.Enabled ?? false;
            set
            {
                if (_manager != null)
                    _manager.Enabled = value;
            }
        }

        [UIValue("beat-division")]
        public float BeatDivision
        {
            get => _manager?.BeatDivision ?? 1.0f;
            set
            {
                if (_manager != null)
                {
                    _manager.BeatDivision = value;
                    NotifyPropertyChanged(nameof(ShowBeatDivisionWarning));
                }
            }
        }

        [UIValue("show-beat-division-warning")]
        public bool ShowBeatDivisionWarning => BeatDivision < 1.0f && ColorMode == 1;

        [UIValue("beat-division-options")]
        public List<object> BeatDivisionOptions => _manager?.BeatDivisionOptions ?? new List<object> { 0.25f, 0.5f, 1.0f, 2.0f };

        [UIValue("color-mode")]
        public int ColorMode
        {
            get => _manager?.ColorMode ?? 1;
            set
            {
                if (_manager != null)
                {
                    _manager.ColorMode = value;
                    NotifyPropertyChanged(nameof(ShowBeatDivisionWarning));
                }
            }
        }

        [UIValue("color-mode-options")]
        public List<object> ColorModeOptions => _manager?.ColorModeOptions ?? new List<object> { 1, 2, 4 };

        [UIValue("color1")]
        public Color Color1
        {
            get => _manager?.Color1 ?? Color.white;
            set
            {
                if (_manager != null) _manager.Color1 = value;
            }
        }

        [UIValue("color2")]
        public Color Color2
        {
            get => _manager?.Color2 ?? Color.white;
            set
            {
                if (_manager != null) _manager.Color2 = value;
            }
        }

        [UIValue("color3")]
        public Color Color3
        {
            get => _manager?.Color3 ?? Color.white;
            set
            {
                if (_manager != null) _manager.Color3 = value;
            }
        }

        [UIValue("color4")]
        public Color Color4
        {
            get => _manager?.Color4 ?? Color.white;
            set
            {
                if (_manager != null) _manager.Color4 = value;
            }
        }

        [UIValue("marker-opacity")]
        public float MarkerOpacity
        {
            get => _manager?.MarkerOpacity ?? 0.8f;
            set
            {
                if (_manager != null) _manager.MarkerOpacity = value;
            }
        }

        [UIValue("marker-size")]
        public float MarkerSize
        {
            get => _manager?.MarkerSize ?? 50f;
            set
            {
                if (_manager != null) _manager.MarkerSize = value;
            }
        }

        [UIValue("flash-duration")]
        public float FlashDuration
        {
            get => _manager?.FlashDuration ?? 0.05f;
            set
            {
                if (_manager != null) _manager.FlashDuration = value;
            }
        }

        [UIValue("marker-z-offset")]
        public float MarkerZOffset
        {
            get => _manager?.MarkerZOffset ?? 1.0f;
            set
            {
                if (_manager != null) _manager.MarkerZOffset = value;
            }
        }

        [UIValue("marker-z-offset-step")]
        public float MarkerZOffsetStep
        {
            get => _manager?.MarkerZOffsetStep ?? 0.1f;
            set
            {
                if (_manager != null) _manager.MarkerZOffsetStep = value;
            }
        }

        [UIValue("marker-z-offset-step-setting")]
        public float MarkerZOffsetStepSetting
        {
            get => _manager?.MarkerZOffsetStep ?? 0.1f;
            set
            {
                if (_manager != null)
                {
                    _manager.MarkerZOffsetStep = value;
                    NotifyPropertyChanged(nameof(MarkerZOffsetStep));
                }
            }
        }

        [UIValue("z-offset-step-options")]
        public List<object> ZOffsetStepOptions => _manager?.ZOffsetStepOptions ?? new List<object> { 0.01f, 0.05f, 0.1f };

        #endregion

        #region Formatters

        [UIAction("beat-division-formatter")]
        public string BeatDivisionFormatter(object value)
        {
            float division = Convert.ToSingle(value);
            if (IsJapanese)
            {
                return division switch
                {
                    0.25f => "1/4ビート",
                    0.5f => "1/2ビート",
                    1.0f => "1ビート",
                    2.0f => "2ビート",
                    _ => $"{division}ビート"
                };
            }
            return division switch
            {
                0.25f => "1/4 beat",
                0.5f => "1/2 beat",
                1.0f => "1 beat",
                2.0f => "2 beats",
                _ => $"{division} beats"
            };
        }

        [UIAction("color-mode-formatter")]
        public string ColorModeFormatter(object value)
        {
            int mode = Convert.ToInt32(value);
            if (IsJapanese)
            {
                return mode switch
                {
                    1 => "1色",
                    2 => "2色",
                    4 => "4色",
                    _ => $"{mode}色"
                };
            }
            return mode switch
            {
                1 => "Single Color",
                2 => "Two Colors",
                4 => "Four Colors",
                _ => $"{mode} Colors"
            };
        }

        [UIAction("z-offset-step-formatter")]
        public string ZOffsetStepFormatter(object value)
        {
            float step = Convert.ToSingle(value);
            return $"{step:F2}";
        }

        #endregion
    }
}
