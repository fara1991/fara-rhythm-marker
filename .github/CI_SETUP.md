# CI/CD Setup Guide

## GitHub Actions Release Workflow

This project includes a GitHub Actions workflow (`release.yml`) that builds the mod and creates GitHub Releases.

## How to Use

1. Go to **Actions** tab in your GitHub repository
2. Select **Build and Release** workflow
3. Click **Run workflow**
4. Configure:
   - **Branch**: Select the branch to build from (dropdown)
   - **Release tag**: Enter version tag (e.g., `v1.0.0`)
   - **Beat Saber version**: Choose `1.29.1`, `1.40.8`, or `both`
5. Click **Run workflow**

## Setting Up Reference DLLs

The build requires Beat Saber reference DLLs. Choose one of these methods:

### Option 1: Repository Secret (Recommended)

1. Create a private repository or file hosting with stripped reference DLLs
2. Organize files as:
   ```
   refs-1.29.1.zip
   refs-1.40.8.zip
   ```
3. Each zip should contain the Beat Saber directory structure:
   ```
   Beat Saber_Data/
     Managed/
       IPA.Loader.dll
       Main.dll
       GameplayCore.dll
       Core.dll
       BeatmapCore.dll
       HMLib.dll
       HMUI.dll
       UnityEngine.dll
       UnityEngine.CoreModule.dll
       UnityEngine.UI.dll
       UnityEngine.UIModule.dll
       UnityEngine.ImageConversionModule.dll
       Unity.TextMeshPro.dll
       Zenject.dll
       Zenject-usage.dll
   Plugins/
     SiraUtil.dll
     BSML.dll
   ```
4. Add repository secret:
   - Go to **Settings** → **Secrets and variables** → **Actions**
   - Add secret `REFS_URL` with the base URL (e.g., `https://your-host.com/refs`)

### Option 2: Libs Folder

1. Create directory structure in your repository:
   ```
   FaraRhythmMarker/
     Libs/
       1.29.1/
         Beat Saber_Data/
           Managed/
             (DLL files)
         Plugins/
           (DLL files)
       1.40.8/
         (same structure)
   ```
2. Add stripped DLLs (you can strip them using tools like `BeatSaberModdingTools`)
3. Commit and push

**Note**: Be careful not to commit copyrighted DLLs. Use stripped versions that only contain public API metadata.

## Required DLLs

### For all versions:
- IPA.Loader.dll
- Main.dll
- GameplayCore.dll
- Core.dll
- BeatmapCore.dll
- HMLib.dll
- HMUI.dll
- UnityEngine.dll
- UnityEngine.CoreModule.dll
- UnityEngine.UI.dll
- UnityEngine.UIModule.dll
- UnityEngine.ImageConversionModule.dll
- Unity.TextMeshPro.dll
- Zenject.dll
- Zenject-usage.dll
- SiraUtil.dll
- BSML.dll

### Additional for 1.40.8:
- DataModels.dll
- BGLib.UnityExtension.dll
- BGLib.AppFlow.dll
- BeatSaber.ViewSystem.dll
