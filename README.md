# FaraRhythmMarker

Beat Saberのプレイ中に、一定のビート間隔で流れてくるリズムマーカーを表示するプラグインです。
ノーツの流れる速度（NJS）と同期してマーカーが移動するため、リズムを視覚的に把握しやすくなります。

## 主な機能

- **動的なリズムマーカー**: 
  - 譜面のNote Jump Speed (NJS) と同期して、奥から手前へ移動します。
  - ノーツのヒット位置（通常はプレイヤーの足元付近、譜面の基本設定に依存）に到達した瞬間に消滅し、正確なリズムガイドとして機能します。
  - 常に4ビート先までのマーカーが表示されるため、リズムの予見が可能です。
- **曲開始との同期**:
  - 曲のカウントダウン終了後、音楽が実際に始まったタイミングを「0ビート目」として正確に同期します。
- **カスタマイズ可能なビート間隔**:
  - 1/4, 1/2, 1, 2ビートの中から表示間隔を選択できます。
- **マルチカラー対応**:
  - 1色（固定）、2色（交互）、4色（サイクル）のカラーモードを選択可能です。
- **サイドガイドライト**:
  - 左右の端に常設の紫色のラインを表示します（Enabled設定に連動）。
- **詳細なビジュアル設定**:
  - マーカーのサイズ、不透明度、色のカスタマイズが可能です。

## 設定項目

ゲーム内の「Mod Settings」メニューから以下の設定を変更できます。

- **Enabled**: プラグインの有効/無効（サイドライトの表示設定も兼ねます）。
- **Beat Division**: マーカーを表示するビート間隔（1/4, 1/2, 1, 2）。
- **Color Mode**: 使用する色の数（1, 2, 4）。
- **Colors (1-4)**: 各スロットの色設定。
- **Marker Opacity**: マーカーの不透明度。
- **Marker Size**: マーカーの大きさ。

## 動作要件

- Beat Saber 1.40.8 (以降のバージョンでも互換性がある可能性があります)
- BSIPA
- SiraUtil
- BeatSaberMarkupLanguage (BSML)

## インストール方法

1. リリースページから最新の `FaraRhythmMarker.dll` をダウンロードします。
2. Beat Saberのインストールフォルダ内の `Plugins` フォルダに配置してください。

---

# FaraRhythmMarker (English)

A Beat Saber plugin that displays rhythm markers flowing at specified beat intervals.
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

## Requirements

- Beat Saber 1.40.8
- BSIPA
- SiraUtil
- BeatSaberMarkupLanguage (BSML)
