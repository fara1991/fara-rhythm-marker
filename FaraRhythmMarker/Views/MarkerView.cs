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
            public float HitTime;
            public Color Color;

            public void Destroy()
            {
                UnityEngine.Object.Destroy(LeftSphere);
                UnityEngine.Object.Destroy(RightSphere);
                UnityEngine.Object.Destroy(Line.gameObject);
            }
        }

        private List<MarkerInstance> _activeMarkers = new List<MarkerInstance>();
        private AudioTimeSyncController? _audioTimeSyncController;
        private GameplayCoreSceneSetupData? _sceneSetupData;
        private PlayerTransforms? _playerTransforms;

        private GameObject? _leftSideLightObj;
        private GameObject? _rightSideLightObj;
        private LineRenderer? _leftSideLine;
        private LineRenderer? _rightSideLine;

        private GameObject? _guideMarkerObj;
        private LineRenderer? _guideLine;

        private float _njs = 10f;

        private Shader? _markerShader;

        public bool IsInitialized { get; private set; }

        public float HitZOffset { get; set; } = 0.5f;

        public void Initialize(AudioTimeSyncController audioTimeSyncController, GameplayCoreSceneSetupData sceneSetupData, PlayerTransforms playerTransforms, float njs)
        {
            try
            {
                _audioTimeSyncController = audioTimeSyncController;
                _sceneSetupData = sceneSetupData;
                _playerTransforms = playerTransforms;
                _njs = njs;

                _markerShader = FindSafeShader("Particles/Additive");
                if (_markerShader == null) _markerShader = FindSafeShader("Unlit/Transparent");

                IsInitialized = true;
                
                if (PluginConfig.Instance.Enabled)
                {
                    CreateSideLights();
                    CreateGuideMarker();
                }

                Plugin.Log.Info($"MarkerView initialized for moving markers. NJS: {_njs}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to initialize MarkerView: {ex.Message}");
            }
        }

        private void CreateSideLights()
        {
            float xOffset = 1.5f;
            float yPos = 0.05f; // 足元 (少しだけ浮かせる)
            Color purple = new Color(0.5f, 0f, 0.5f);

            _leftSideLightObj = CreateConstantLightLine("FaraSideLight_L", new Vector3(-xOffset, yPos, 0), purple, out _leftSideLine);
            _rightSideLightObj = CreateConstantLightLine("FaraSideLight_R", new Vector3(xOffset, yPos, 0), purple, out _rightSideLine);
        }

        private void CreateGuideMarker()
        {
            float xOffset = 1.5f;
            float yPos = 0.05f;
            Color white = Color.white;

            _guideMarkerObj = new GameObject("FaraGuideMarker");
            _guideLine = _guideMarkerObj.AddComponent<LineRenderer>();
            // ガイドマーカーは常に表示され、奥に配置するため ZWrite=true, renderQueue=2900 にする
            SetupLineRenderer(_guideLine, true);
            
            // ガイドマーカーは常に表示されるので、不透明度を設定
            float alpha = PluginConfig.Instance.MarkerOpacity * 0.8f; // 少し控えめに表示
            Color guideColor = new Color(white.r, white.g, white.b, alpha);
            
            _guideLine.startColor = guideColor;
            _guideLine.endColor = guideColor;
            _guideLine.startWidth = 0.03f; // 動くマーカー(0.05)より少し細くする
            _guideLine.endWidth = 0.03f;

            // 位置はUpdateMarkersでHitZOffsetに合わせて設定される
            _guideLine.SetPosition(0, new Vector3(-xOffset, yPos, HitZOffset));
            _guideLine.SetPosition(1, new Vector3(xOffset, yPos, HitZOffset));

            UnityEngine.Object.DontDestroyOnLoad(_guideMarkerObj);
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

            // ライト効果を追加 (中央付近に配置するか、複数学べるか)
            // 要件では「同じようなライトを使い」とのことなので、Lineに加えて点光源も置くか
            var lightObj = new GameObject(name + "_Light");
            lightObj.transform.SetParent(obj.transform);
            lightObj.transform.localPosition = new Vector3(basePosition.x, basePosition.y, 0);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.color = color;
            light.intensity = 2f;

            UnityEngine.Object.DontDestroyOnLoad(obj);
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

            var marker = new MarkerInstance
            {
                HitTime = hitTime,
                Color = GetColorForIndex(colorIndex)
            };

            // マーカーの作成
            marker.LeftSphere = CreateMarkerSphere("FaraMarker_L", Vector3.zero);
            marker.RightSphere = CreateMarkerSphere("FaraMarker_R", Vector3.zero);
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

        private GameObject CreateMarkerSphere(string name, Vector3 position)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.position = position;
            sphere.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null && _markerShader != null)
            {
                var mat = new Material(_markerShader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3010; // ライン(3000)よりさらに手前に表示
                renderer.material = mat;
            }

            var light = sphere.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            
            UnityEngine.Object.DontDestroyOnLoad(sphere);
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
                float xOffset = 1.5f;

                if (_leftSideLine != null)
                {
                    _leftSideLine.SetPosition(0, new Vector3(-xOffset, yPos, zStart));
                    _leftSideLine.SetPosition(1, new Vector3(-xOffset, yPos, zEnd));
                }
                if (_rightSideLine != null)
                {
                    _rightSideLine.SetPosition(0, new Vector3(xOffset, yPos, zStart));
                    _rightSideLine.SetPosition(1, new Vector3(xOffset, yPos, zEnd));
                }

                if (_guideLine != null)
                {
                    _guideLine.SetPosition(0, new Vector3(-xOffset, yPos, HitZOffset));
                    _guideLine.SetPosition(1, new Vector3(xOffset, yPos, HitZOffset));
                }
            }

            for (int i = _activeMarkers.Count - 1; i >= 0; i--)
            {
                var marker = _activeMarkers[i];
                if (songTime >= marker.HitTime)
                {
                    marker.Destroy();
                    _activeMarkers.RemoveAt(i);
                    continue;
                }

                UpdateMarkerPosition(marker, songTime);
            }
        }

        private void UpdateMarkerPosition(MarkerInstance marker, float songTime)
        {
            // 残り時間
            float timeLeft = marker.HitTime - songTime;
            
            // Z位置の計算: ヒート位置(台座の前) + 残り時間 * NJS
            // マーカーが奥(正のZ)から手前(台座の前)に向かって移動するようにする
            // Note: songTime が実時間であれば timeScale は不要だが、
            // songTime が曲内時間の場合、NJS は曲内時間あたりの速度である必要がある。
            float currentNjs = _njs;
            if (_audioTimeSyncController != null)
            {
                // 音ゲーの再生速度 (1.0, 1.2, 1.5, 0.85 など) を考慮
                // NJS (Note Jump Speed) は実秒あたりの移動速度 (m/s)
                // timeLeft は「曲内秒数」であるため、実秒に変換するために timeScale で割る必要がある
                // 実秒 = 曲内秒 / timeScale
                // zPos = (実秒 * NJS) + Offset
                currentNjs /= _audioTimeSyncController.timeScale;
            }
            float zPos = (timeLeft * currentNjs) + HitZOffset;

            float xOffset = 1.5f;
            float yPos = 0.05f;

            Vector3 leftPos = new Vector3(-xOffset, yPos, zPos);
            Vector3 rightPos = new Vector3(xOffset, yPos, zPos);

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

            marker.LeftSphere.GetComponent<Renderer>().material.color = colorWithAlpha;
            marker.RightSphere.GetComponent<Renderer>().material.color = colorWithAlpha;
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

            if (_leftSideLightObj != null) UnityEngine.Object.Destroy(_leftSideLightObj);
            if (_rightSideLightObj != null) UnityEngine.Object.Destroy(_rightSideLightObj);
            if (_guideMarkerObj != null) UnityEngine.Object.Destroy(_guideMarkerObj);

            IsInitialized = false;
        }

        // 互換性のための空メソッド
        public void UpdateFlash(float deltaTime) { }
        public void TriggerFlash(int colorIndex) { }
    }
}
