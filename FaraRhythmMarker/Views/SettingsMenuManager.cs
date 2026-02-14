using System;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using FaraRhythmMarker.Configuration;
using FaraRhythmMarker.Services;
using Zenject;

namespace FaraRhythmMarker.Views
{
    internal class SettingsMenuManager : IInitializable, IDisposable
    {
        private MenuButton? _menuButton;
        private SettingsModalView? _modalView;
        private MenuPreviewService? _previewService;

        private string _language = "English";
        private bool _enabled;
        private float _beatDivision;
        private int _colorMode;
        private UnityEngine.Color _color1;
        private UnityEngine.Color _color2;
        private UnityEngine.Color _color3;
        private UnityEngine.Color _color4;
        private float _markerOpacity;
        private float _markerSize;
        private float _flashDuration;
        private float _markerZOffset;
        private float _markerZOffsetStep;

        #region UI Values

        [BeatSaberMarkupLanguage.Attributes.UIValue("language-options")]
        public List<object> LanguageOptions => new List<object> { "English", "Japanese" };

        [BeatSaberMarkupLanguage.Attributes.UIValue("language")]
        public string Language
        {
            get => _language;
            set
            {
                _language = value;
                PluginConfig.Instance.Language = value;
                PluginConfig.Instance.Changed();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("enabled")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PluginConfig.Instance.Enabled = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateEnabled(value);
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("beat-division")]
        public float BeatDivision
        {
            get => _beatDivision;
            set
            {
                _beatDivision = value;
                PluginConfig.Instance.BeatDivision = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateBeatDivision();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("beat-division-options")]
        public List<object> BeatDivisionOptions => new List<object> { 0.25f, 0.5f, 1.0f, 2.0f };

        [BeatSaberMarkupLanguage.Attributes.UIValue("color-mode")]
        public int ColorMode
        {
            get => _colorMode;
            set
            {
                _colorMode = value;
                PluginConfig.Instance.ColorMode = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateColors();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("color-mode-options")]
        public List<object> ColorModeOptions => new List<object> { 1, 2, 4 };

        [BeatSaberMarkupLanguage.Attributes.UIValue("color1")]
        public UnityEngine.Color Color1
        {
            get => _color1;
            set
            {
                _color1 = value;
                PluginConfig.Instance.Color1R = value.r;
                PluginConfig.Instance.Color1G = value.g;
                PluginConfig.Instance.Color1B = value.b;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateColors();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("color2")]
        public UnityEngine.Color Color2
        {
            get => _color2;
            set
            {
                _color2 = value;
                PluginConfig.Instance.Color2R = value.r;
                PluginConfig.Instance.Color2G = value.g;
                PluginConfig.Instance.Color2B = value.b;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateColors();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("color3")]
        public UnityEngine.Color Color3
        {
            get => _color3;
            set
            {
                _color3 = value;
                PluginConfig.Instance.Color3R = value.r;
                PluginConfig.Instance.Color3G = value.g;
                PluginConfig.Instance.Color3B = value.b;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateColors();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("color4")]
        public UnityEngine.Color Color4
        {
            get => _color4;
            set
            {
                _color4 = value;
                PluginConfig.Instance.Color4R = value.r;
                PluginConfig.Instance.Color4G = value.g;
                PluginConfig.Instance.Color4B = value.b;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateColors();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("marker-opacity")]
        public float MarkerOpacity
        {
            get => _markerOpacity;
            set
            {
                _markerOpacity = value;
                PluginConfig.Instance.MarkerOpacity = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateMarkerOpacity(value);
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("marker-size")]
        public float MarkerSize
        {
            get => _markerSize;
            set
            {
                _markerSize = value;
                PluginConfig.Instance.MarkerSize = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateMarkerSize(value);
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("flash-duration")]
        public float FlashDuration
        {
            get => _flashDuration;
            set
            {
                _flashDuration = value;
                PluginConfig.Instance.FlashDuration = value;
                PluginConfig.Instance.Changed();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("marker-z-offset")]
        public float MarkerZOffset
        {
            get => _markerZOffset;
            set
            {
                _markerZOffset = value;
                PluginConfig.Instance.MarkerZOffset = value;
                PluginConfig.Instance.Changed();
                _previewService?.UpdateMarkerZOffset(value);
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("marker-z-offset-step")]
        public float MarkerZOffsetStep
        {
            get => _markerZOffsetStep;
            set
            {
                _markerZOffsetStep = value;
                PluginConfig.Instance.MarkerZOffsetStep = value;
                PluginConfig.Instance.Changed();
            }
        }

        [BeatSaberMarkupLanguage.Attributes.UIValue("z-offset-step-options")]
        public List<object> ZOffsetStepOptions => new List<object> { 0.01f, 0.05f, 0.1f };

        #endregion

        public void Initialize()
        {
            LoadCurrentSettings();
            RegisterSettingsMenu();
        }

        private void LoadCurrentSettings()
        {
            _language = PluginConfig.Instance.Language;
            _enabled = PluginConfig.Instance.Enabled;
            _beatDivision = PluginConfig.Instance.BeatDivision;
            _colorMode = PluginConfig.Instance.ColorMode;
            _color1 = new UnityEngine.Color(PluginConfig.Instance.Color1R, PluginConfig.Instance.Color1G, PluginConfig.Instance.Color1B);
            _color2 = new UnityEngine.Color(PluginConfig.Instance.Color2R, PluginConfig.Instance.Color2G, PluginConfig.Instance.Color2B);
            _color3 = new UnityEngine.Color(PluginConfig.Instance.Color3R, PluginConfig.Instance.Color3G, PluginConfig.Instance.Color3B);
            _color4 = new UnityEngine.Color(PluginConfig.Instance.Color4R, PluginConfig.Instance.Color4G, PluginConfig.Instance.Color4B);
            _markerOpacity = PluginConfig.Instance.MarkerOpacity;
            _markerSize = PluginConfig.Instance.MarkerSize;
            _flashDuration = PluginConfig.Instance.FlashDuration;
            _markerZOffset = PluginConfig.Instance.MarkerZOffset;
            _markerZOffsetStep = PluginConfig.Instance.MarkerZOffsetStep;
        }

        private void RegisterSettingsMenu()
        {
            try
            {
                _menuButton = new MenuButton("Fara Rhythm Marker", "Configure rhythm marker settings", ShowSettings);
#if BS_1_29_1
                MenuButtons.instance.RegisterButton(_menuButton);
#else
                MenuButtons.Instance.RegisterButton(_menuButton);
#endif
                Plugin.Log.Info("Menu button registered");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to register menu button: {ex}");
            }
        }

        private void ShowSettings()
        {
            try
            {
                Plugin.Log.Info("ShowSettings called");

                if (_modalView == null)
                {
                    _modalView = BeatSaberUI.CreateViewController<SettingsModalView>();
                    _modalView.Setup(this);
                    Plugin.Log.Info("Modal view created");
                }

                _previewService?.Dispose();
                _previewService = new MenuPreviewService();

                var flowCoordinator = BeatSaberUI.CreateFlowCoordinator<SettingsFlowCoordinator>();
                flowCoordinator.SetViewController(_modalView);
                flowCoordinator.SetPreviewService(_previewService);

                BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(
                    flowCoordinator,
                    null,
                    HMUI.ViewController.AnimationDirection.Horizontal,
                    false,
                    false);

                Plugin.Log.Info("Flow coordinator presented");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to show settings: {ex}");
            }
        }

        public void Dispose()
        {
            try
            {
                _previewService?.Dispose();
                _previewService = null;

                if (_menuButton != null)
                {
#if BS_1_29_1
                    var menuButtons = MenuButtons.instance;
#else
                    var menuButtons = MenuButtons.Instance;
#endif
                    if (menuButtons != null)
                    {
                        menuButtons.UnregisterButton(_menuButton);
                    }
                }

                if (_modalView != null && _modalView.gameObject != null)
                {
                    UnityEngine.Object.Destroy(_modalView.gameObject);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Debug($"MenuButton cleanup skipped (app quitting): {ex.Message}");
            }
        }
    }
}
