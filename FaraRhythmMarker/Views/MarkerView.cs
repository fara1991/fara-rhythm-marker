using System;
using System.Collections.Generic;
using FaraRhythmMarker.Configuration;
using UnityEngine;

namespace FaraRhythmMarker.Views
{
    /// <summary>
    /// Handles the visual representation of moving rhythm markers
    /// </summary>
    internal class MarkerView : IDisposable
    {
        private class MarkerInstance
        {
            public GameObject LeftSphere = null!;
            public GameObject RightSphere = null!;
            public LineRenderer Line = null!;
            public Light LeftLight = null!;
            public Light RightLight = null!;
            public Renderer LeftRenderer = null!;
            public Renderer RightRenderer = null!;
            public float HitTime;
            public Color Color;
            public float Bpm;  // このマーカーのヒット時点でのBPM

            public void Destroy()
            {
                // Materialを先に破棄
                if (LeftRenderer != null && LeftRenderer.material != null)
                    UnityEngine.Object.Destroy(LeftRenderer.material);
                if (RightRenderer != null && RightRenderer.material != null)
                    UnityEngine.Object.Destroy(RightRenderer.material);
                if (Line != null && Line.material != null)
                    UnityEngine.Object.Destroy(Line.material);

                if (LeftSphere != null)
                    UnityEngine.Object.Destroy(LeftSphere);
                if (RightSphere != null)
                    UnityEngine.Object.Destroy(RightSphere);
                if (Line != null && Line.gameObject != null)
                    UnityEngine.Object.Destroy(Line.gameObject);
            }
        }

        private readonly List<MarkerInstance> _activeMarkers = new List<MarkerInstance>();
        private AudioTimeSyncController? _audioTimeSyncController;
        private PlayerTransforms? _playerTransforms;

        private GameObject? _leftSideLightObj;
        private GameObject? _rightSideLightObj;
        private LineRenderer? _leftSideLine;
        private LineRenderer? _rightSideLine;

        private GameObject? _guideMarkerObj;
        private LineRenderer? _guideLine;
        private GameObject? _guideLeftSphere;
        private GameObject? _guideRightSphere;

        // フラッシュエフェクト用
        private GameObject? _flashMarkerObj;
        private LineRenderer? _flashLine;
        private GameObject? _flashLeftSphere;
        private GameObject? _flashRightSphere;
        private Renderer? _flashLeftRenderer;
        private Renderer? _flashRightRenderer;
        private Light? _flashLeftLight;
        private Light? _flashRightLight;
        private bool _isFlashing = false;
        private float _flashTimer = 0f;
        private float _flashDuration = 0.1f;
        private Color _flashColor = Color.white;
        private const float FlashStartScale = 1.0f;
        private const float FlashEndScale = 2.0f;

        private float _njs = 10f;
        private float _baseBpm = 120f;
        private Func<float, float>? _getBpmAtTime;

        private Shader? _markerShader;

        public bool IsInitialized { get; private set; }

        public float HitZOffset { get; set; } = 0.5f;

        /// <summary>
        /// 足場の左右端のX座標オフセット（足場の半幅）
        /// </summary>
        public float PlatformXOffset { get; set; } = 1.5f;

        public void Initialize(AudioTimeSyncController audioTimeSyncController, PlayerTransforms playerTransforms, float njs, float baseBpm = 120f, Func<float, float>? getBpmAtTime = null)
        {
            try
            {
                _audioTimeSyncController = audioTimeSyncController;
                _playerTransforms = playerTransforms;
                _njs = njs;
                _baseBpm = baseBpm;
                _getBpmAtTime = getBpmAtTime;

                _markerShader = FindSafeShader("Particles/Additive");
                if (_markerShader == null) _markerShader = FindSafeShader("Unlit/Transparent");

                IsInitialized = true;

                if (PluginConfig.Instance.Enabled)
                {
                    CreateSideLights();
                    CreateGuideMarker();
                    CreateFlashMarker();
                }

                Plugin.Log.Info($"MarkerView initialized for moving markers. NJS: {_njs}, BaseBPM: {_baseBpm}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to initialize MarkerView: {ex.Message}");
            }
        }

        private void CreateSideLights()
        {
            float yPos = 0.05f; // 足元 (少しだけ浮かせる)
            Color purple = new Color(0.5f, 0f, 0.5f);

            _leftSideLightObj = CreateConstantLightLine("FaraSideLight_L", new Vector3(-PlatformXOffset, yPos, 0), purple, out _leftSideLine);
            _rightSideLightObj = CreateConstantLightLine("FaraSideLight_R", new Vector3(PlatformXOffset, yPos, 0), purple, out _rightSideLine);
        }

        private void CreateGuideMarker()
        {
            float yPos = 0.05f;
            Color white = Color.white;
            float alpha = PluginConfig.Instance.MarkerOpacity * 0.8f; // 少し控えめに表示
            Color guideColor = new Color(white.r, white.g, white.b, alpha);

            _guideMarkerObj = new GameObject("FaraGuideMarker");
            _guideLine = _guideMarkerObj.AddComponent<LineRenderer>();
            // ガイドマーカーは常に表示され、奥に配置するため ZWrite=true, renderQueue=2900 にする
            SetupLineRenderer(_guideLine, true);

            _guideLine.startColor = guideColor;
            _guideLine.endColor = guideColor;
            _guideLine.startWidth = 0.1f; // Y方向の幅
            _guideLine.endWidth = 0.1f;

            // 位置はUpdateMarkersでHitZOffsetとPlatformXOffsetに合わせて設定される
            _guideLine.SetPosition(0, new Vector3(-PlatformXOffset, yPos, HitZOffset));
            _guideLine.SetPosition(1, new Vector3(PlatformXOffset, yPos, HitZOffset));

            // 左右に球体を追加（移動マーカーと同様）
            Vector3 leftPos = new Vector3(-PlatformXOffset, yPos, HitZOffset);
            Vector3 rightPos = new Vector3(PlatformXOffset, yPos, HitZOffset);

            _guideLeftSphere = CreateGuideSphere("FaraGuideMarker_L", leftPos, guideColor);
            _guideRightSphere = CreateGuideSphere("FaraGuideMarker_R", rightPos, guideColor);

        }

        private GameObject CreateGuideSphere(string name, Vector3 position, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.position = position;
            sphere.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            // Colliderを削除（物理演算の負荷軽減）
            var collider = sphere.GetComponent("SphereCollider");
            if (collider != null) UnityEngine.Object.Destroy(collider);

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null && _markerShader != null)
            {
                var mat = new Material(_markerShader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 1);
                mat.renderQueue = 2900;
                mat.color = color;
                renderer.material = mat;
            }

            var light = sphere.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            light.color = color;
            light.intensity = 2f;

            return sphere;
        }

        private void CreateFlashMarker()
        {
            float yPos = 0.05f;

            _flashMarkerObj = new GameObject("FaraFlashMarker");
            _flashLine = _flashMarkerObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_flashLine, false); // フラッシュは手前に表示

            // 初期状態は非表示
            _flashLine.startColor = new Color(1, 1, 1, 0);
            _flashLine.endColor = new Color(1, 1, 1, 0);
            _flashLine.startWidth = 0.1f; // Y方向の幅
            _flashLine.endWidth = 0.1f;

            _flashLine.SetPosition(0, new Vector3(-PlatformXOffset, yPos, HitZOffset));
            _flashLine.SetPosition(1, new Vector3(PlatformXOffset, yPos, HitZOffset));

            // 左右に球体を追加（初期状態は非表示）
            Vector3 leftPos = new Vector3(-PlatformXOffset, yPos, HitZOffset);
            Vector3 rightPos = new Vector3(PlatformXOffset, yPos, HitZOffset);

            _flashLeftSphere = CreateFlashSphere("FaraFlashMarker_L", leftPos, out var leftRenderer, out var leftLight);
            _flashRightSphere = CreateFlashSphere("FaraFlashMarker_R", rightPos, out var rightRenderer, out var rightLight);
            _flashLeftRenderer = leftRenderer;
            _flashRightRenderer = rightRenderer;
            _flashLeftLight = leftLight;
            _flashRightLight = rightLight;

        }

        private GameObject CreateFlashSphere(string name, Vector3 position, out Renderer outRenderer, out Light outLight)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.position = position;
            sphere.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            // Colliderを削除（物理演算の負荷軽減）
            var collider = sphere.GetComponent("SphereCollider");
            if (collider != null) UnityEngine.Object.Destroy(collider);

            outRenderer = sphere.GetComponent<Renderer>();
            if (outRenderer != null && _markerShader != null)
            {
                var mat = new Material(_markerShader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3010;
                mat.color = new Color(1, 1, 1, 0); // 初期状態は透明
                outRenderer.material = mat;
            }

            outLight = sphere.AddComponent<Light>();
            outLight.type = LightType.Point;
            outLight.range = 2f;
            outLight.intensity = 0f; // 初期状態はオフ

            return sphere;
        }

        /// <summary>
        /// ビートタイミングでフラッシュエフェクトを発動
        /// </summary>
        public void TriggerBeatFlash(Color color)
        {
            _isFlashing = true;
            _flashTimer = 0f;
            _flashColor = color;
            _flashDuration = PluginConfig.Instance.FlashDuration > 0 ? PluginConfig.Instance.FlashDuration : 0.1f;
        }

        /// <summary>
        /// フラッシュエフェクトの更新
        /// </summary>
        private void UpdateFlashEffect()
        {
            if (!_isFlashing || _flashLine == null) return;

            _flashTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_flashTimer / _flashDuration);

            // イージング（徐々に速くなる）
            float easedProgress = progress * progress;

            // スケール: 1.0 → 2.0 (拡大)
            float scale = Mathf.Lerp(FlashStartScale, FlashEndScale, easedProgress);

            // アルファ: 1.0 → 0.0 (フェードアウト)
            float alpha = 1.0f - easedProgress;

            // 色とアルファを設定
            Color flashColorWithAlpha = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);
            _flashLine.startColor = flashColorWithAlpha;
            _flashLine.endColor = flashColorWithAlpha;

            // 幅をスケーリング（拡大エフェクト）- Y方向
            float baseWidth = 0.1f;
            _flashLine.startWidth = baseWidth * scale;
            _flashLine.endWidth = baseWidth * scale;

            // 位置を更新
            float yPos = 0.05f;
            float scaledXOffset = PlatformXOffset * scale;
            _flashLine.SetPosition(0, new Vector3(-scaledXOffset, yPos, HitZOffset));
            _flashLine.SetPosition(1, new Vector3(scaledXOffset, yPos, HitZOffset));

            // 球体の更新（キャッシュされたコンポーネントを使用）
            float baseSphereScale = 0.15f;
            float sphereScale = baseSphereScale * scale;

            if (_flashLeftSphere != null)
            {
                _flashLeftSphere.transform.position = new Vector3(-scaledXOffset, yPos, HitZOffset);
                _flashLeftSphere.transform.localScale = new Vector3(sphereScale, sphereScale, sphereScale);
                if (_flashLeftRenderer != null)
                {
                    _flashLeftRenderer.material.color = flashColorWithAlpha;
                }
                if (_flashLeftLight != null)
                {
                    _flashLeftLight.color = _flashColor;
                    _flashLeftLight.intensity = alpha * 5f;
                }
            }

            if (_flashRightSphere != null)
            {
                _flashRightSphere.transform.position = new Vector3(scaledXOffset, yPos, HitZOffset);
                _flashRightSphere.transform.localScale = new Vector3(sphereScale, sphereScale, sphereScale);
                if (_flashRightRenderer != null)
                {
                    _flashRightRenderer.material.color = flashColorWithAlpha;
                }
                if (_flashRightLight != null)
                {
                    _flashRightLight.color = _flashColor;
                    _flashRightLight.intensity = alpha * 5f;
                }
            }

            // フラッシュ終了
            if (progress >= 1.0f)
            {
                _isFlashing = false;
                // 完全に透明にする
                _flashLine.startColor = new Color(1, 1, 1, 0);
                _flashLine.endColor = new Color(1, 1, 1, 0);

                if (_flashLeftRenderer != null)
                {
                    _flashLeftRenderer.material.SetColor("_Color", new Color(1, 1, 1, 0));
                }
                if (_flashLeftLight != null)
                {
                    _flashLeftLight.intensity = 0f;
                }
                if (_flashRightRenderer != null)
                {
                    _flashRightRenderer.material.SetColor("_Color", new Color(1, 1, 1, 0));
                }
                if (_flashRightLight != null)
                {
                    _flashRightLight.intensity = 0f;
                }
            }
        }

        private GameObject CreateConstantLightLine(string name, Vector3 basePosition, Color color, out LineRenderer line)
        {
            var obj = new GameObject(name);
            line = obj.AddComponent<LineRenderer>();
            SetupLineRenderer(line, true); // サイドライトも奥(下地)として扱う
            
            // 初期位置 (Updateでプレイヤー位置に合わせて更新される)
            line.SetPosition(0, new Vector3(basePosition.x, basePosition.y, -1f));
            line.SetPosition(1, new Vector3(basePosition.x, basePosition.y, 40f));
            
            line.startColor = color;
            line.endColor = color;
            line.startWidth = 0.02f;
            line.endWidth = 0.02f;

            // ライト効果を追加 (中央付近に配置するか、複数並べるか)
            // Lineに加えて点光源も配置
            var lightObj = new GameObject(name + "_Light");
            lightObj.transform.SetParent(obj.transform);
            lightObj.transform.localPosition = new Vector3(basePosition.x, basePosition.y, 0);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.color = color;
            light.intensity = 2f;

            return obj;
        }

        private Shader? FindSafeShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader != null) return shader;

            // Fallbacks
            shader = Shader.Find("UI/Default");
            if (shader != null) return shader;

            shader = Shader.Find("Sprites/Default");
            if (shader != null) return shader;

            // Final fallback: Use the shader from a primitive
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var result = temp.GetComponent<Renderer>().sharedMaterial.shader;
            UnityEngine.Object.Destroy(temp);
            return result;
        }

        public void SpawnMarker(int colorIndex, float hitTime)
        {
            if (!IsInitialized) return;

            // このマーカーのヒット時点でのBPMを取得
            float markerBpm = _baseBpm;
            if (_getBpmAtTime != null)
            {
                markerBpm = _getBpmAtTime(hitTime);
            }

            var marker = new MarkerInstance
            {
                HitTime = hitTime,
                Color = GetColorForIndex(colorIndex),
                Bpm = markerBpm
            };

            // マーカーの作成
            marker.LeftSphere = CreateMarkerSphere("FaraMarker_L", Vector3.zero, out var leftRenderer);
            marker.RightSphere = CreateMarkerSphere("FaraMarker_R", Vector3.zero, out var rightRenderer);
            marker.LeftRenderer = leftRenderer;
            marker.RightRenderer = rightRenderer;
            marker.LeftLight = marker.LeftSphere.GetComponent<Light>();
            marker.RightLight = marker.RightSphere.GetComponent<Light>();

            var lineObj = new GameObject("FaraMarker_Line");
            marker.Line = lineObj.AddComponent<LineRenderer>();
            SetupLineRenderer(marker.Line);

            _activeMarkers.Add(marker);

            if (_audioTimeSyncController != null)
                UpdateMarkerPosition(marker, _audioTimeSyncController.songTime);
        }

        private void SetupLineRenderer(LineRenderer line, bool writeZ = false)
        {
            if (_markerShader != null)
            {
                var mat = new Material(_markerShader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", writeZ ? 1 : 0);
                mat.renderQueue = writeZ ? 2900 : 3000; // writeZが真なら少し手前のキューにする
                line.material = mat;
            }
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.positionCount = 2;
        }

        private GameObject CreateMarkerSphere(string name, Vector3 position, out Renderer outRenderer)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.position = position;
            sphere.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            // Colliderを削除（物理演算の負荷軽減）
            var collider = sphere.GetComponent("SphereCollider");
            if (collider != null) UnityEngine.Object.Destroy(collider);

            outRenderer = sphere.GetComponent<Renderer>();
            if (outRenderer != null && _markerShader != null)
            {
                var mat = new Material(_markerShader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3010; // ライン(3000)よりさらに手前に表示
                outRenderer.material = mat;
            }

            var light = sphere.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;

            return sphere;
        }

        public void UpdateMarkers()
        {
            if (!IsInitialized || _audioTimeSyncController == null) return;

            float songTime = _audioTimeSyncController.songTime;

            // プレイヤーのZ座標に合わせてサイドライトを更新
            if (PluginConfig.Instance.Enabled)
            {
                float zStart = -1f;
                float zEnd = 40f;
                float yPos = 0.05f;

                if (_leftSideLine != null)
                {
                    _leftSideLine.SetPosition(0, new Vector3(-PlatformXOffset, yPos, zStart));
                    _leftSideLine.SetPosition(1, new Vector3(-PlatformXOffset, yPos, zEnd));
                }
                if (_rightSideLine != null)
                {
                    _rightSideLine.SetPosition(0, new Vector3(PlatformXOffset, yPos, zStart));
                    _rightSideLine.SetPosition(1, new Vector3(PlatformXOffset, yPos, zEnd));
                }

                if (_guideLine != null)
                {
                    _guideLine.SetPosition(0, new Vector3(-PlatformXOffset, yPos, HitZOffset));
                    _guideLine.SetPosition(1, new Vector3(PlatformXOffset, yPos, HitZOffset));
                }

                // ガイドマーカーの球体位置も更新
                if (_guideLeftSphere != null)
                {
                    _guideLeftSphere.transform.position = new Vector3(-PlatformXOffset, yPos, HitZOffset);
                }
                if (_guideRightSphere != null)
                {
                    _guideRightSphere.transform.position = new Vector3(PlatformXOffset, yPos, HitZOffset);
                }
            }

            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _activeMarkers[i];

                // まず位置を更新（消滅直前でもガイドマーカー位置に正確に配置するため）
                UpdateMarkerPosition(marker, songTime);

                // ヒットタイムを過ぎたら消滅
                if (songTime >= marker.HitTime)
                {
                    // ビートタイミングでフラッシュエフェクトを発動
                    TriggerBeatFlash(marker.Color);

                    marker.Destroy();
                    _activeMarkers.RemoveAt(i);
                }
            }

            // フラッシュエフェクトの更新
            UpdateFlashEffect();
        }

        private void UpdateMarkerPosition(MarkerInstance marker, float songTime)
        {
            // 残り時間（0以下にならないようにクランプ）
            float timeLeft = Mathf.Max(0f, marker.HitTime - songTime);

            // Z位置の計算: ヒート位置(台座の前) + 残り時間 * 調整済みNJS
            // マーカーが奥(正のZ)から手前(台座の前)に向かって移動するようにする

            // ベースNJSをBPM比率で調整
            // BPMが高いほどマーカーが速く移動する
            float bpmRatio = marker.Bpm / _baseBpm;
            float currentNjs = _njs * bpmRatio;

            if (_audioTimeSyncController != null)
            {
                // 音ゲーの再生速度 (1.0, 1.2, 1.5, 0.85 など) を考慮
                // NJS (Note Jump Speed) は実秒あたりの移動速度 (m/s)
                // timeLeft は「曲内秒数」であるため、実秒に変換するために timeScale で割る必要がある
                currentNjs /= _audioTimeSyncController.timeScale;
            }
            float zPos = (timeLeft * currentNjs) + HitZOffset;

            float yPos = 0.05f;

            Vector3 leftPos = new Vector3(-PlatformXOffset, yPos, zPos);
            Vector3 rightPos = new Vector3(PlatformXOffset, yPos, zPos);

            marker.LeftSphere.transform.position = leftPos;
            marker.RightSphere.transform.position = rightPos;

            marker.Line.SetPosition(0, leftPos);
            marker.Line.SetPosition(1, rightPos);

            // 可視性の更新 (透明度など)
            float alpha = PluginConfig.Instance.MarkerOpacity;
            
            // 手前に来すぎたら消え始めるようにする
            if (timeLeft < 0.1f)
            {
                alpha *= Mathf.Max(0, timeLeft / 0.1f);
            }

            // 3ビート先(生成時)からフェードインさせる場合などはここで調整可能
            // 現在は出現時は不透明（設定値）で、消える直前にフェードアウトする実装

            Color colorWithAlpha = new Color(marker.Color.r, marker.Color.g, marker.Color.b, alpha);

            marker.LeftRenderer.material.color = colorWithAlpha;
            marker.RightRenderer.material.color = colorWithAlpha;
            marker.Line.startColor = colorWithAlpha;
            marker.Line.endColor = colorWithAlpha;

            marker.LeftLight.color = marker.Color;
            marker.LeftLight.intensity = alpha * 5f;
            marker.RightLight.color = marker.Color;
            marker.RightLight.intensity = alpha * 5f;
        }

        private Color GetColorForIndex(int index)
        {
            var config = PluginConfig.Instance;
            return index switch
            {
                0 => new Color(config.Color1R, config.Color1G, config.Color1B),
                1 => new Color(config.Color2R, config.Color2G, config.Color2B),
                2 => new Color(config.Color3R, config.Color3G, config.Color3B),
                3 => new Color(config.Color4R, config.Color4G, config.Color4B),
                _ => new Color(config.Color1R, config.Color1G, config.Color1B)
            };
        }

        public void Dispose()
        {
            foreach (var marker in _activeMarkers)
            {
                marker.Destroy();
            }
            _activeMarkers.Clear();

            // Materialを先に破棄（GPUリソースの解放）
            if (_leftSideLine != null && _leftSideLine.material != null)
                UnityEngine.Object.Destroy(_leftSideLine.material);
            if (_rightSideLine != null && _rightSideLine.material != null)
                UnityEngine.Object.Destroy(_rightSideLine.material);
            if (_guideLine != null && _guideLine.material != null)
                UnityEngine.Object.Destroy(_guideLine.material);
            if (_flashLine != null && _flashLine.material != null)
                UnityEngine.Object.Destroy(_flashLine.material);
            if (_flashLeftRenderer != null && _flashLeftRenderer.material != null)
                UnityEngine.Object.Destroy(_flashLeftRenderer.material);
            if (_flashRightRenderer != null && _flashRightRenderer.material != null)
                UnityEngine.Object.Destroy(_flashRightRenderer.material);

            // ガイドマーカーの球体のMaterial
            if (_guideLeftSphere != null)
            {
                var renderer = _guideLeftSphere.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                    UnityEngine.Object.Destroy(renderer.material);
            }
            if (_guideRightSphere != null)
            {
                var renderer = _guideRightSphere.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                    UnityEngine.Object.Destroy(renderer.material);
            }

            if (_leftSideLightObj != null) UnityEngine.Object.Destroy(_leftSideLightObj);
            if (_rightSideLightObj != null) UnityEngine.Object.Destroy(_rightSideLightObj);
            if (_guideMarkerObj != null) UnityEngine.Object.Destroy(_guideMarkerObj);
            if (_guideLeftSphere != null) UnityEngine.Object.Destroy(_guideLeftSphere);
            if (_guideRightSphere != null) UnityEngine.Object.Destroy(_guideRightSphere);
            if (_flashMarkerObj != null) UnityEngine.Object.Destroy(_flashMarkerObj);
            if (_flashLeftSphere != null) UnityEngine.Object.Destroy(_flashLeftSphere);
            if (_flashRightSphere != null) UnityEngine.Object.Destroy(_flashRightSphere);

            // キャッシュされた参照をクリア
            _leftSideLightObj = null;
            _rightSideLightObj = null;
            _leftSideLine = null;
            _rightSideLine = null;
            _guideMarkerObj = null;
            _guideLine = null;
            _guideLeftSphere = null;
            _guideRightSphere = null;
            _flashMarkerObj = null;
            _flashLine = null;
            _flashLeftSphere = null;
            _flashRightSphere = null;
            _flashLeftRenderer = null;
            _flashRightRenderer = null;
            _flashLeftLight = null;
            _flashRightLight = null;

            _isFlashing = false;
            IsInitialized = false;
        }
    }
}
