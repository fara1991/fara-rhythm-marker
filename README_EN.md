# FaraRhythmMarker

A Beat Saber (1.29.1 / 1.40.8) plugin that displays rhythm markers flowing at specified beat intervals.
The markers move in sync with the song's Note Jump Speed (NJS), helping you visualize the rhythm more effectively.

## Features

- **Dynamic Rhythm Markers**:
  - Fully synchronized with Note Jump Speed (NJS).
  - Markers move from the distance and disappear exactly at the note hit position (usually near the player's feet, depending on map settings).
  - Displays markers up to 4 beats ahead for better preparation.
- **BPM Change Support**:
  - Automatically detects mid-song BPM change events.
  - Marker movement speed adjusts automatically when BPM changes.
- **Song Start Sync**:
  - Automatically detects the actual song start and sets it as "Beat 0".
- **Customizable Beat Divisions**:
  - Choose between 1/4, 1/2, 1, or 2 beat intervals.
- **Multiple Color Modes**:
  - Cycle through 1, 2, or 4 different colors.
- **Side Guide Lights**:
  - Constant purple lines on both sides to help with orientation (controlled by the Enabled setting).
- **Visual Customization**:
  - Adjust marker size, opacity, flash duration, and individual colors.
  - Z-offset adjustment for audio latency compensation.

## Settings

You can change the following settings from the "Mod Settings" menu in-game.

- **Enabled**: Enable/Disable the plugin (also toggles the side guide lights).
- **Beat Division**: The beat interval for displaying markers (1/4, 1/2, 1, 2).
- **Color Mode**: Number of colors to use (1, 2, 4).
- **Colors (1-4)**: Color settings for each slot.
- **Marker Opacity**: Opacity of the markers.
- **Marker Size**: Size of the markers.
- **Flash Duration**: How long the marker flashes at hit position (seconds).
- **Marker Z Offset**: Z-axis offset for audio latency compensation.
- **Z Offset Step**: Step size for Z offset adjustment.

## Requirements

### Beat Saber 1.29.1
| Mod | Version |
|-----|---------|
| BSIPA | 4.2.0+ |
| SiraUtil | 3.1.0+ |
| BeatSaberMarkupLanguage (BSML) | 1.6.0+ |

### Beat Saber 1.40.8
| Mod | Version |
|-----|---------|
| BSIPA | 4.3.0+ |
| SiraUtil | 3.1.0+ |
| BeatSaberMarkupLanguage (BSML) | 1.11.0+ |

## Installation

1. Download `FaraRhythmMarker-vX.X.X-BSXXX.zip` for your Beat Saber version from the [releases page](../../releases).
2. Extract the zip and place `FaraRhythmMarker.dll` in the `Plugins` folder inside your Beat Saber installation directory.

## Building

To build for a specific version, use `dotnet build` with the `BSVersion` property.

```bash
# For 1.29.1 (Default)
dotnet build -p:BSVersion=1.29.1

# For 1.40.8
dotnet build -p:BSVersion=1.40.8
```

Note: Make sure to update the `<BeatSaberDir_1_29_1>` and `<BeatSaberDir_1_40_8>` in `FaraRhythmMarker.csproj` to point to your Beat Saber installation directories before building.

### Version-specific Dependencies (manifest.json)
The appropriate `manifest.json` is embedded in the DLL based on the `BSVersion` specified during build.
- For `1.29.1`: Uses `manifest.1.29.1.json`, depends on BSML 1.6.0+.
- For `1.40.8`: Uses `manifest.1.40.8.json`, depends on BSML 1.11.0+.

To support new versions, create a `manifest.<version>.json` file and it will be automatically used.

## GitHub Actions

This repository includes a GitHub Actions workflow for automated releases.

1. Go to the **Actions** tab on GitHub and select **Build and Release**
2. Click **Run workflow**
3. Specify branch, tag (e.g., `v1.0.0`), and Beat Saber version
4. The DLL will be automatically uploaded to the Releases page

See [.github/CI_SETUP.md](.github/CI_SETUP.md) for details.

## License

MIT License
