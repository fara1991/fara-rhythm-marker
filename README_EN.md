# FaraRhythmMarker

A Beat Saber (1.29.1 / 1.40.8) plugin that displays rhythm markers flowing at specified beat intervals.
The markers move in sync with the song's Note Jump Speed (NJS), helping you visualize the rhythm more effectively.

## Features

- **Dynamic Rhythm Markers**:
  - Fully synchronized with Note Jump Speed (NJS).
  - Markers move from the distance and disappear exactly at the note hit position (usually near the player's feet, depending on map settings).
  - Displays markers up to 4 beats ahead for better preparation.
- **Song Start Sync**:
  - Automatically detects the actual song start and sets it as "Beat 0".
- **Customizable Beat Divisions**:
  - Choose between 1/4, 1/2, 1, or 2 beat intervals.
- **Multiple Color Modes**:
  - Cycle through 1, 2, or 4 different colors.
- **Side Guide Lights**:
  - Constant purple lines on both sides to help with orientation (controlled by the Enabled setting).
- **Visual Customization**:
  - Adjust marker size, opacity, and individual colors.

## Settings

You can change the following settings from the "Mod Settings" menu in-game.

- **Enabled**: Enable/Disable the plugin (also toggles the side guide lights).
- **Beat Division**: The beat interval for displaying markers (1/4, 1/2, 1, 2).
- **Color Mode**: Number of colors to use (1, 2, 4).
- **Colors (1-4)**: Color settings for each slot.
- **Marker Opacity**: Opacity of the markers.
- **Marker Size**: Size of the markers.

## Requirements

- Beat Saber 1.29.1 / 1.40.8
- BSIPA
- SiraUtil
- BeatSaberMarkupLanguage (BSML)

## Installation

1. Download the latest `FaraRhythmMarker.dll` from the releases page.
2. Place it in the `Plugins` folder inside your Beat Saber installation directory.
