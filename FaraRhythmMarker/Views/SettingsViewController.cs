using System;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.ViewControllers;
using FaraRhythmMarker.Configuration;
using UnityEngine;
using Zenject;

namespace FaraRhythmMarker.Views
{
    /// <summary>
    /// Handles the settings menu UI and data binding
    /// </summary>
    public class SettingsMenuManager : IInitializable, IDisposable
    {
        private MenuButton? _menuButton;
        private SettingsModalView? _modalView;

        private bool _enabled;
        private float _beatDivision;
        private int _colorMode;
        private Color _color1;
        private Color _color2;
        private Color _color3;
        private Color _color4;
        private float _markerOpacity;
        private float _markerSize;
        private float _flashDuration;
        private float _markerZOffset;
        private float _markerZOffsetStep;

        public SettingsMenuManager()
        {
        }

        #region UI Values

        [UIValue("enabled")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PluginConfig.Instance.Enabled = value;
                Plugin.Log.Info($"[Menu] Setting Enabled to: {value}, config hash: {PluginConfig.Instance.GetHashCode()}");
                PluginConfig.Instance.Changed();
                Plugin.Log.Info($"[Menu] After Changed(): Enabled={PluginConfig.Instance.Enabled}");
            }
        }

        [UIValue("beat-division")]
        public float BeatDivision
        {
            get => _beatDivision;
            set
            {
                _beatDivision = value;
                PluginConfig.Instance.BeatDivision = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("marker-opacity")]
        public float MarkerOpacity
        {
            get => _markerOpacity;
            set
            {
                _markerOpacity = value;
                PluginConfig.Instance.MarkerOpacity = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("marker-size")]
        public float MarkerSize
        {
            get => _markerSize;
            set
            {
                _markerSize = value;
                PluginConfig.Instance.MarkerSize = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("flash-duration")]
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

        [UIValue("beat-division-options")]
        public System.Collections.Generic.List<object> BeatDivisionOptions => new()
        {
            0.25f, 0.5f, 1.0f, 2.0f
        };

        [UIValue("color-mode")]
        public int ColorMode
        {
            get => _colorMode;
            set
            {
                _colorMode = value;
                PluginConfig.Instance.ColorMode = value;
                Plugin.Log.Info($"[Menu] Setting ColorMode to: {value}, config hash: {PluginConfig.Instance.GetHashCode()}");
                PluginConfig.Instance.Changed();
                Plugin.Log.Info($"[Menu] After Changed(): ColorMode={PluginConfig.Instance.ColorMode}");
            }
        }

        [UIValue("color-mode-options")]
        public System.Collections.Generic.List<object> ColorModeOptions => new()
        {
            1, 2, 4
        };

        [UIValue("marker-z-offset")]
        public float MarkerZOffset
        {
            get => _markerZOffset;
            set
            {
                _markerZOffset = value;
                PluginConfig.Instance.MarkerZOffset = value;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("marker-z-offset-step")]
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

        [UIValue("z-offset-step-options")]
        public System.Collections.Generic.List<object> ZOffsetStepOptions => new()
        {
            0.01f, 0.05f, 0.1f
        };

        [UIValue("color1")]
        public Color Color1
        {
            get => _color1;
            set
            {
                _color1 = value;
                PluginConfig.Instance.Color1R = value.r;
                PluginConfig.Instance.Color1G = value.g;
                PluginConfig.Instance.Color1B = value.b;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("color2")]
        public Color Color2
        {
            get => _color2;
            set
            {
                _color2 = value;
                PluginConfig.Instance.Color2R = value.r;
                PluginConfig.Instance.Color2G = value.g;
                PluginConfig.Instance.Color2B = value.b;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("color3")]
        public Color Color3
        {
            get => _color3;
            set
            {
                _color3 = value;
                PluginConfig.Instance.Color3R = value.r;
                PluginConfig.Instance.Color3G = value.g;
                PluginConfig.Instance.Color3B = value.b;
                PluginConfig.Instance.Changed();
            }
        }

        [UIValue("color4")]
        public Color Color4
        {
            get => _color4;
            set
            {
                _color4 = value;
                PluginConfig.Instance.Color4R = value.r;
                PluginConfig.Instance.Color4G = value.g;
                PluginConfig.Instance.Color4B = value.b;
                PluginConfig.Instance.Changed();
            }
        }

        #endregion

        [UIAction("beat-division-formatter")]
        public string BeatDivisionFormatter(object value)
        {
            float division = Convert.ToSingle(value);
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

        public void Initialize()
        {
            LoadCurrentSettings();
            RegisterSettingsMenu();
        }

        private void LoadCurrentSettings()
        {
            _enabled = PluginConfig.Instance.Enabled;
            _beatDivision = PluginConfig.Instance.BeatDivision;
            _colorMode = PluginConfig.Instance.ColorMode;
            _color1 = new Color(PluginConfig.Instance.Color1R, PluginConfig.Instance.Color1G, PluginConfig.Instance.Color1B);
            _color2 = new Color(PluginConfig.Instance.Color2R, PluginConfig.Instance.Color2G, PluginConfig.Instance.Color2B);
            _color3 = new Color(PluginConfig.Instance.Color3R, PluginConfig.Instance.Color3G, PluginConfig.Instance.Color3B);
            _color4 = new Color(PluginConfig.Instance.Color4R, PluginConfig.Instance.Color4G, PluginConfig.Instance.Color4B);
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
                BeatSaberMarkupLanguage.MenuButtons.MenuButtons.instance.RegisterButton(_menuButton);
#else
                BeatSaberMarkupLanguage.MenuButtons.MenuButtons.Instance.RegisterButton(_menuButton);
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

                var flowCoordinator = BeatSaberUI.CreateFlowCoordinator<SettingsFlowCoordinator>();
                flowCoordinator.SetViewController(_modalView);

                BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(flowCoordinator, null, HMUI.ViewController.AnimationDirection.Horizontal, false, false);
                Plugin.Log.Info("Flow coordinator presented");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to show settings: {ex}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public void Dispose()
        {
            if (_menuButton != null)
            {
                try
                {
#if BS_1_29_1
                    var menuButtons = BeatSaberMarkupLanguage.MenuButtons.MenuButtons.instance;
#else
                    var menuButtons = BeatSaberMarkupLanguage.MenuButtons.MenuButtons.Instance;
#endif
                    if (menuButtons != null)
                    {
                        menuButtons.UnregisterButton(_menuButton);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Debug($"MenuButton cleanup skipped (app quitting): {ex.Message}");
                }
            }

            if (_modalView != null && _modalView.gameObject != null)
            {
                UnityEngine.Object.Destroy(_modalView.gameObject);
            }
        }
    }

    [ViewDefinition("FaraRhythmMarker.Views.SettingsView.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\SettingsView.bsml")]
    internal class SettingsModalView : BSMLAutomaticViewController
    {
        private SettingsMenuManager? _manager;

        public void Setup(SettingsMenuManager manager)
        {
            _manager = manager;
        }

        [UIValue("enabled")]
        public bool Enabled
        {
            get => _manager?.Enabled ?? false;
            set
            {
                Plugin.Log.Info($"[ModalView] Enabled setter called with value: {value}");
                if (_manager != null)
                {
                    _manager.Enabled = value;
                }
                else
                {
                    Plugin.Log.Error("[ModalView] Manager is null!");
                }
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

        [UIValue("marker-opacity")]
        public float MarkerOpacity
        {
            get => _manager?.MarkerOpacity ?? 0.5f;
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
            get => _manager?.FlashDuration ?? 0.1f;
            set
            {
                if (_manager != null) _manager.FlashDuration = value;
            }
        }

        [UIValue("beat-division-options")]
        public System.Collections.Generic.List<object> BeatDivisionOptions => new()
        {
            0.25f, 0.5f, 1.0f, 2.0f
        };

        [UIValue("color-mode")]
        public int ColorMode
        {
            get => _manager?.ColorMode ?? 1;
            set
            {
                Plugin.Log.Info($"[ModalView] ColorMode setter called with value: {value}");
                if (_manager != null)
                {
                    _manager.ColorMode = value;
                    NotifyPropertyChanged(nameof(ShowBeatDivisionWarning));
                }
                else
                {
                    Plugin.Log.Error("[ModalView] Manager is null!");
                }
            }
        }

        [UIValue("color-mode-options")]
        public System.Collections.Generic.List<object> ColorModeOptions => new()
        {
            1, 2, 4
        };

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
        public System.Collections.Generic.List<object> ZOffsetStepOptions => new()
        {
            0.01f, 0.05f, 0.1f
        };

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

        [UIAction("beat-division-formatter")]
        public string BeatDivisionFormatter(object value)
        {
            float division = Convert.ToSingle(value);
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
    }

    internal class SettingsFlowCoordinator : HMUI.FlowCoordinator
    {
        private SettingsModalView? _viewController;

        public void SetViewController(SettingsModalView viewController)
        {
            _viewController = viewController;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    SetTitle("Fara Rhythm Marker");
                    showBackButton = true;
                }

                if (_viewController != null)
                {
                    ProvideInitialViewControllers(_viewController);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error in SettingsFlowCoordinator.DidActivate: {ex}");
            }
        }

        protected override void BackButtonWasPressed(HMUI.ViewController topViewController)
        {
            try
            {
                BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error dismissing flow coordinator: {ex}");
            }
        }
    }
}
