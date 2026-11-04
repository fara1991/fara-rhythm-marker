---
name: fara-rhythm-marker-patterns
description: FaraRhythmMarker（Beat Saber 1.29.1・1.40.8 用の、拍のマーカーを出す Mod）のコードを変更するときの構成と決まり。版ごとの分岐・設定・描画・ビルドを扱うときに使う。
---

# FaraRhythmMarker の構成と決まり

プレイ中に拍のマーカーを表示する Beat Saber の Mod（公開リポジトリ）。対象は 1.29.1 と 1.40.8（.NET Framework 4.8）。依存は BSIPA・SiraUtil・BSML。機能と設定は README.md（英語版は README_EN.md。片方を直したら、もう片方も直す）と SETTINGS_GUIDE.md。

## 構成

```
FaraRhythmMarker/
├── Plugin.cs                         # 入口。[Plugin(RuntimeOptions.DynamicInit)]、Zenject の Installer を登録
├── Configuration/PluginConfig.cs     # IPA の Config Store（virtual のプロパティで自動保存）
├── Controllers/RhythmMarkerController.cs  # プレイ中の処理（IInitializable・ITickable・IDisposable）
├── Models/BeatTimingModel.cs         # 拍の計算（Unity に依存しない）
├── Views/
│   ├── MarkerView.cs                 # マーカーの描画（GameObject・LineRenderer・シェーダー）
│   ├── SettingsView.1.29.1.bsml / SettingsView.1.40.8.bsml   # 設定画面（版ごと）
│   └── SettingsFlowCoordinator.cs・SettingsMenuManager.cs・SettingsModalView.cs
├── Services/MenuPreviewService.cs    # 設定画面のプレビュー
├── Installers/                       # RhythmMarkerGameInstaller（GameCore）・RhythmMarkerMenuInstaller（Menu）
└── manifest.json・manifest.1.29.1.json・manifest.1.40.8.json
```

## 版ごとの違い

- csproj が `BSVersion` に応じて `BS_1_29_1` か `BS_1_40_8` を定義する。ゲームの API の違いは `#if BS_1_29_1 … #else … #endif` で分ける（例: 1.29.1 は `IDifficultyBeatmap`、1.40.8 は `BeatmapLevel`）
- 版ごとの manifest と bsml は、csproj が `BSVersion` で選んで埋め込む。版を上げるときは、`version` を 3 つの manifest すべてでそろえる

## 描画

- シェーダーは `MarkerView.FindSafeShader` で探す（`Particles/Additive` → `Unlit/Transparent` → `UI/Default`・`Sprites/Default` → プリミティブの材質）。版によって見つからないシェーダーがあるため
- 作った GameObject・Material は `Dispose` で破棄する

## ビルド

```powershell
dotnet build   # 既定（1.29.1）のビルドのあと、1.40.8 も続けてビルドし、各 Beat Saber の Plugins にコピーする
```

- Beat Saber の場所は csproj の `BeatSaberDir_<版>`（`$(USERPROFILE)\BSManager\BSInstances\<版>`）。CI は `CIBuild=true` で `Refs\<版>` を参照する
- FaraAccField が、このプラグインの DLL を参照する（グリッドの奥行きを合わせる設定）。公開の型・設定を変えたら、FaraAccField のビルドも確かめる

## その他

- ドメインの計算の説明は、日本語のコメントで書く
- 公開リポジトリなので、個人のパス・秘密情報は書かない
