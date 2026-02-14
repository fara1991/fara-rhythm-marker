using System;
using System.Collections.Generic;
using FaraRhythmMarker.Configuration;
using UnityEngine;

namespace FaraRhythmMarker.Services
{
    internal class MenuPreviewService : IDisposable
    {
        private const float PlatformXOffset = 1.5f;
        private const float BaseSphereScale = 0.15f;
        private const float FlashStartScale = 1.0f;
        private const float FlashEndScale = 2.0f;
        private const float BasePreviewDistance = 2.0f;
        private const float PreviewBPM = 120f;
        private const float SpawnZDistance = 5.0f;
        private const float MarkerSpeed = 3.0f;
        private const float YPos = 0.5f;
        private const int MaxMarkers = 30;
        private const float DefaultMarkerSize = 50.0f;
        private const float GuideOpacityMultiplier = 0.8f;
        private const float GuideLightIntensity = 2.5f;
        private const float MarkerLightIntensity = 5f;
        private const float FadeStartDistance = 0.3f;
        private const float SideLightLineWidth = 0.02f;
        private const float GuideLineWidth = 0.1f;
        private const float DefaultLineWidth = 0.05f;
        private const float SideLightRange = 5f;
        private const float SphereLightRange = 2f;
        private const float SideLightIntensity = 2f;
        private const float DefaultFlashDuration = 0.1f;

        private GameObject? _container;
        private bool _isShowing;
        private Shader? _shader;
        private float _cameraZ;
        private float _guideZ;

        // Side lights
        private GameObject? _leftSideLightObj;
        private GameObject? _rightSideLightObj;

        // Guide marker
        private LineRenderer? _guideLine;
        private GameObject? _guideLeftSphere;
        private GameObject? _guideRightSphere;
        private Renderer? _guideLeftRenderer;
        private Renderer? _guideRightRenderer;
        private Light? _guideLeftLight;
        private Light? _guideRightLight;

        // Moving markers
        private class PreviewMarker
        {
            public GameObject Container = null!;
            public LineRenderer Line = null!;
            public GameObject LeftSphere = null!;
            public GameObject RightSphere = null!;
            public Renderer LeftRenderer = null!;
            public Renderer RightRenderer = null!;
            public Light LeftLight = null!;
            public Light RightLight = null!;
            public Color Color;
            public bool IsFlashing;
            public float FlashTimer;
        }

        private readonly List<PreviewMarker> _markers = new();
        private float _spawnTimer;
        private int _colorIndex;

        private class PreviewUpdater : MonoBehaviour
        {
            public MenuPreviewService? Service;

            private void Update()
            {
                Service?.Tick();
            }
        }

        public void Show()
        {
            if (_isShowing)
                return;

            try
            {
                _container = new GameObject("RhythmMarker_MenuPreviewContainer");

                var updater = _container.AddComponent<PreviewUpdater>();
                updater.Service = this;

                var cam = Camera.main;
                _cameraZ = cam != null ? cam.transform.position.z : 0f;
                _guideZ = _cameraZ + BasePreviewDistance + PluginConfig.Instance.MarkerZOffset;

                _shader = FindShader();
                if (_shader == null)
                {
                    Plugin.Log?.Info("MenuPreviewService: Could not find suitable shader");
                    UnityEngine.Object.Destroy(_container);
                    _container = null;
                    return;
                }

                CreateSideLights();
                CreateGuideMarker();

                _spawnTimer = 0f;
                _colorIndex = 0;
                _isShowing = true;

                if (!PluginConfig.Instance.Enabled)
                    _container.SetActive(false);

                Plugin.Log?.Info("MenuPreviewService: Show");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"MenuPreviewService Show failed: {ex}");
            }
        }

        private static Shader? FindShader()
        {
            return Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Particles/Additive");
        }

        private float GetSpawnInterval()
        {
            float beatDivision = PluginConfig.Instance.BeatDivision;
            return beatDivision * 60.0f / PreviewBPM;
        }

        private void CreateSideLights()
        {
            Color purple = new Color(0.5f, 0f, 0.5f);
            float zStart = _guideZ - 1f;
            float zEnd = _guideZ + SpawnZDistance + 1f;

            _leftSideLightObj = CreateSideLightLine("RhythmMarker_SideLight_L",
                new Vector3(-PlatformXOffset, YPos, zStart),
                new Vector3(-PlatformXOffset, YPos, zEnd),
                purple);

            _rightSideLightObj = CreateSideLightLine("RhythmMarker_SideLight_R",
                new Vector3(PlatformXOffset, YPos, zStart),
                new Vector3(PlatformXOffset, YPos, zEnd),
                purple);
        }

        private GameObject CreateSideLightLine(string name, Vector3 start, Vector3 end, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(_container!.transform, true);

            var line = obj.AddComponent<LineRenderer>();
            SetupLineRenderer(line, true);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startColor = color;
            line.endColor = color;
            line.startWidth = SideLightLineWidth;
            line.endWidth = SideLightLineWidth;

            var lightObj = new GameObject(name + "_Light");
            lightObj.transform.SetParent(obj.transform);
            lightObj.transform.position = (start + end) * 0.5f;
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = SideLightRange;
            light.color = color;
            light.intensity = SideLightIntensity;

            return obj;
        }

        private void CreateGuideMarker()
        {
            float alpha = PluginConfig.Instance.MarkerOpacity * GuideOpacityMultiplier;
            Color guideColor = new Color(1f, 1f, 1f, alpha);

            var guideObj = new GameObject("RhythmMarker_GuideMarker");
            guideObj.transform.SetParent(_container!.transform, true);

            _guideLine = guideObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_guideLine, true);
            _guideLine.startColor = guideColor;
            _guideLine.endColor = guideColor;
            _guideLine.startWidth = GuideLineWidth;
            _guideLine.endWidth = GuideLineWidth;
            _guideLine.SetPosition(0, new Vector3(-PlatformXOffset, YPos, _guideZ));
            _guideLine.SetPosition(1, new Vector3(PlatformXOffset, YPos, _guideZ));

            float sphereScale = GetSphereScale();
            _guideLeftSphere = CreateSphere("RhythmMarker_Guide_L",
                new Vector3(-PlatformXOffset, YPos, _guideZ), guideColor, sphereScale, true,
                out _guideLeftRenderer, out _guideLeftLight);
            _guideRightSphere = CreateSphere("RhythmMarker_Guide_R",
                new Vector3(PlatformXOffset, YPos, _guideZ), guideColor, sphereScale, true,
                out _guideRightRenderer, out _guideRightLight);
        }

        private GameObject CreateSphere(string name, Vector3 position, Color color,
            float scale, bool writeZ, out Renderer outRenderer, out Light outLight)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(_container!.transform, true);
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * scale;

            var collider = sphere.GetComponent("SphereCollider");
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            outRenderer = sphere.GetComponent<Renderer>();
            if (outRenderer != null && _shader != null)
            {
                var mat = new Material(_shader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", writeZ ? 1 : 0);
                mat.renderQueue = writeZ ? 2900 : 3010;
                mat.color = color;
                outRenderer.material = mat;
            }

            outLight = sphere.AddComponent<Light>();
            outLight.type = LightType.Point;
            outLight.range = SphereLightRange;
            outLight.color = color;
            outLight.intensity = color.a * GuideLightIntensity;

            return sphere;
        }

        private void SetupLineRenderer(LineRenderer line, bool writeZ)
        {
            if (_shader != null)
            {
                var mat = new Material(_shader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", writeZ ? 1 : 0);
                mat.renderQueue = writeZ ? 2900 : 3000;
                line.material = mat;
            }
            line.startWidth = DefaultLineWidth;
            line.endWidth = DefaultLineWidth;
            line.positionCount = 2;
        }

        private float GetSphereScale()
        {
            return BaseSphereScale * (PluginConfig.Instance.MarkerSize / DefaultMarkerSize);
        }

        private Color GetColorForIndex(int index)
        {
            var config = PluginConfig.Instance;
            int colorMode = config.ColorMode > 0 ? config.ColorMode : 1;
            int wrappedIndex = index % colorMode;
            return wrappedIndex switch
            {
                0 => new Color(config.Color1R, config.Color1G, config.Color1B),
                1 => new Color(config.Color2R, config.Color2G, config.Color2B),
                2 => new Color(config.Color3R, config.Color3G, config.Color3B),
                3 => new Color(config.Color4R, config.Color4G, config.Color4B),
                _ => new Color(config.Color1R, config.Color1G, config.Color1B)
            };
        }

        private void SpawnMarker()
        {
            if (_container == null || _shader == null)
                return;

            if (_markers.Count >= MaxMarkers)
                return;

            Color color = GetColorForIndex(_colorIndex);
            int colorMode = PluginConfig.Instance.ColorMode > 0 ? PluginConfig.Instance.ColorMode : 1;
            _colorIndex = (_colorIndex + 1) % colorMode;

            float alpha = PluginConfig.Instance.MarkerOpacity;
            Color colorWithAlpha = new Color(color.r, color.g, color.b, alpha);
            float spawnZ = _guideZ + SpawnZDistance;

            var marker = CreatePreviewMarker(colorWithAlpha, color, spawnZ);
            _markers.Add(marker);
        }

        private PreviewMarker CreatePreviewMarker(Color colorWithAlpha, Color baseColor, float spawnZ)
        {
            float sphereScale = GetSphereScale();

            var container = new GameObject("RhythmMarker_PreviewMarker");
            container.transform.SetParent(_container!.transform, true);

            var line = CreateMarkerLine(container, colorWithAlpha, spawnZ);

            var leftSphere = CreateMarkerSphere("RhythmMarker_PreviewMarker_L",
                new Vector3(-PlatformXOffset, YPos, spawnZ), colorWithAlpha, sphereScale,
                out var leftRenderer, out var leftLight);
            leftSphere.transform.SetParent(container.transform, true);

            var rightSphere = CreateMarkerSphere("RhythmMarker_PreviewMarker_R",
                new Vector3(PlatformXOffset, YPos, spawnZ), colorWithAlpha, sphereScale,
                out var rightRenderer, out var rightLight);
            rightSphere.transform.SetParent(container.transform, true);

            return new PreviewMarker
            {
                Container = container,
                Line = line,
                LeftSphere = leftSphere,
                RightSphere = rightSphere,
                LeftRenderer = leftRenderer,
                RightRenderer = rightRenderer,
                LeftLight = leftLight,
                RightLight = rightLight,
                Color = baseColor
            };
        }

        private LineRenderer CreateMarkerLine(GameObject parent, Color color, float z)
        {
            var lineObj = new GameObject("RhythmMarker_PreviewMarker_Line");
            lineObj.transform.SetParent(parent.transform, true);
            var line = lineObj.AddComponent<LineRenderer>();
            SetupLineRenderer(line, false);
            line.startColor = color;
            line.endColor = color;
            line.SetPosition(0, new Vector3(-PlatformXOffset, YPos, z));
            line.SetPosition(1, new Vector3(PlatformXOffset, YPos, z));
            return line;
        }

        private GameObject CreateMarkerSphere(string name, Vector3 position, Color color,
            float scale, out Renderer outRenderer, out Light outLight)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * scale;

            var collider = sphere.GetComponent("SphereCollider");
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            outRenderer = sphere.GetComponent<Renderer>();
            if (outRenderer != null && _shader != null)
            {
                var mat = new Material(_shader);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3010;
                mat.color = color;
                outRenderer.material = mat;
            }

            outLight = sphere.AddComponent<Light>();
            outLight.type = LightType.Point;
            outLight.range = SphereLightRange;
            outLight.color = new Color(color.r, color.g, color.b, 1f);
            outLight.intensity = color.a * MarkerLightIntensity;

            return sphere;
        }

        private void Tick()
        {
            if (!_isShowing)
                return;

            float dt = Time.deltaTime;

            _spawnTimer += dt;
            float interval = GetSpawnInterval();
            if (_spawnTimer >= interval)
            {
                _spawnTimer -= interval;
                SpawnMarker();
            }

            float alpha = PluginConfig.Instance.MarkerOpacity;
            float flashDuration = PluginConfig.Instance.FlashDuration > 0
                ? PluginConfig.Instance.FlashDuration : DefaultFlashDuration;

            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var marker = _markers[i];
                if (marker.Container == null)
                {
                    _markers.RemoveAt(i);
                    continue;
                }

                if (marker.IsFlashing)
                {
                    if (TickFlashing(marker, dt, flashDuration))
                    {
                        DestroyMarker(marker);
                        _markers.RemoveAt(i);
                    }
                }
                else
                {
                    TickMoving(marker, dt, alpha);
                }
            }
        }

        private bool TickFlashing(PreviewMarker marker, float dt, float flashDuration)
        {
            marker.FlashTimer += dt;
            float progress = Mathf.Clamp01(marker.FlashTimer / flashDuration);
            float easedProgress = progress * progress;

            float scale = Mathf.Lerp(FlashStartScale, FlashEndScale, easedProgress);
            float flashAlpha = 1.0f - easedProgress;

            Color flashColor = new Color(marker.Color.r, marker.Color.g, marker.Color.b, flashAlpha);

            marker.Line.startColor = flashColor;
            marker.Line.endColor = flashColor;

            marker.Line.startWidth = GuideLineWidth * scale;
            marker.Line.endWidth = GuideLineWidth * scale;

            float scaledXOffset = PlatformXOffset * scale;
            marker.Line.SetPosition(0, new Vector3(-scaledXOffset, YPos, _guideZ));
            marker.Line.SetPosition(1, new Vector3(scaledXOffset, YPos, _guideZ));

            float sphereScale = GetSphereScale() * scale;
            marker.LeftSphere.transform.position = new Vector3(-scaledXOffset, YPos, _guideZ);
            marker.LeftSphere.transform.localScale = Vector3.one * sphereScale;
            marker.RightSphere.transform.position = new Vector3(scaledXOffset, YPos, _guideZ);
            marker.RightSphere.transform.localScale = Vector3.one * sphereScale;

            marker.LeftRenderer.material.color = flashColor;
            marker.RightRenderer.material.color = flashColor;
            marker.LeftLight.color = marker.Color;
            marker.LeftLight.intensity = flashAlpha * MarkerLightIntensity;
            marker.RightLight.color = marker.Color;
            marker.RightLight.intensity = flashAlpha * MarkerLightIntensity;

            return progress >= 1.0f;
        }

        private void TickMoving(PreviewMarker marker, float dt, float alpha)
        {
            float leftZ = marker.LeftSphere.transform.position.z - MarkerSpeed * dt;

            if (leftZ <= _guideZ)
            {
                marker.IsFlashing = true;
                marker.FlashTimer = 0f;

                marker.LeftSphere.transform.position = new Vector3(-PlatformXOffset, YPos, _guideZ);
                marker.RightSphere.transform.position = new Vector3(PlatformXOffset, YPos, _guideZ);
                marker.Line.SetPosition(0, new Vector3(-PlatformXOffset, YPos, _guideZ));
                marker.Line.SetPosition(1, new Vector3(PlatformXOffset, YPos, _guideZ));
                return;
            }

            Vector3 leftPos = new Vector3(-PlatformXOffset, YPos, leftZ);
            Vector3 rightPos = new Vector3(PlatformXOffset, YPos, leftZ);

            marker.LeftSphere.transform.position = leftPos;
            marker.RightSphere.transform.position = rightPos;
            marker.Line.SetPosition(0, leftPos);
            marker.Line.SetPosition(1, rightPos);

            float distToGuide = leftZ - _guideZ;
            float markerAlpha = alpha;
            if (distToGuide < FadeStartDistance)
                markerAlpha *= distToGuide / FadeStartDistance;

            Color colorWithAlpha = new Color(marker.Color.r, marker.Color.g, marker.Color.b, markerAlpha);
            marker.Line.startColor = colorWithAlpha;
            marker.Line.endColor = colorWithAlpha;
            marker.LeftRenderer.material.color = colorWithAlpha;
            marker.RightRenderer.material.color = colorWithAlpha;
            marker.LeftLight.color = marker.Color;
            marker.LeftLight.intensity = markerAlpha * MarkerLightIntensity;
            marker.RightLight.color = marker.Color;
            marker.RightLight.intensity = markerAlpha * MarkerLightIntensity;
        }

        private static void DestroyMarker(PreviewMarker marker)
        {
            DestroyMaterial(marker.LeftRenderer);
            DestroyMaterial(marker.RightRenderer);
            DestroyLineRendererMaterial(marker.Line);
            if (marker.Container != null)
                UnityEngine.Object.Destroy(marker.Container);
        }

        public void Hide()
        {
            if (!_isShowing)
                return;

            foreach (var marker in _markers)
                DestroyMarker(marker);
            _markers.Clear();

            DestroyMaterial(_guideLeftRenderer);
            DestroyMaterial(_guideRightRenderer);
            DestroyLineRendererMaterial(_guideLine);
            DestroyLineRendererMaterial(_leftSideLightObj?.GetComponent<LineRenderer>());
            DestroyLineRendererMaterial(_rightSideLightObj?.GetComponent<LineRenderer>());

            if (_container != null)
            {
                UnityEngine.Object.Destroy(_container);
                _container = null;
            }

            _leftSideLightObj = null;
            _rightSideLightObj = null;
            _guideLine = null;
            _guideLeftSphere = null;
            _guideRightSphere = null;
            _guideLeftRenderer = null;
            _guideRightRenderer = null;
            _guideLeftLight = null;
            _guideRightLight = null;
            _isShowing = false;

            Plugin.Log?.Info("MenuPreviewService: Hide");
        }

        private static void DestroyMaterial(Renderer? renderer)
        {
            if (renderer != null && renderer.material != null)
                UnityEngine.Object.Destroy(renderer.material);
        }

        private static void DestroyLineRendererMaterial(LineRenderer? line)
        {
            if (line != null && line.material != null)
                UnityEngine.Object.Destroy(line.material);
        }

        public void UpdateEnabled(bool enabled)
        {
            if (_container != null)
                _container.SetActive(enabled);
        }

        public void UpdateMarkerOpacity(float opacity)
        {
            if (!_isShowing)
                return;

            float guideAlpha = opacity * GuideOpacityMultiplier;
            Color guideColor = new Color(1f, 1f, 1f, guideAlpha);

            if (_guideLine != null)
            {
                _guideLine.startColor = guideColor;
                _guideLine.endColor = guideColor;
            }
            if (_guideLeftRenderer != null)
                _guideLeftRenderer.material.color = guideColor;
            if (_guideRightRenderer != null)
                _guideRightRenderer.material.color = guideColor;
            if (_guideLeftLight != null)
                _guideLeftLight.intensity = guideAlpha * GuideLightIntensity;
            if (_guideRightLight != null)
                _guideRightLight.intensity = guideAlpha * GuideLightIntensity;
        }

        public void UpdateMarkerSize(float size)
        {
            if (!_isShowing)
                return;

            float scale = BaseSphereScale * (size / DefaultMarkerSize);

            if (_guideLeftSphere != null)
                _guideLeftSphere.transform.localScale = Vector3.one * scale;
            if (_guideRightSphere != null)
                _guideRightSphere.transform.localScale = Vector3.one * scale;

            foreach (var marker in _markers)
            {
                if (marker.IsFlashing) continue;
                if (marker.LeftSphere != null)
                    marker.LeftSphere.transform.localScale = Vector3.one * scale;
                if (marker.RightSphere != null)
                    marker.RightSphere.transform.localScale = Vector3.one * scale;
            }
        }

        public void UpdateMarkerZOffset(float zOffset)
        {
            if (!_isShowing)
                return;

            float oldGuideZ = _guideZ;
            _guideZ = _cameraZ + BasePreviewDistance + zOffset;
            float delta = _guideZ - oldGuideZ;

            UpdateGuidePosition();
            UpdateSideLightPositions();
            ShiftMarkersZ(delta);
        }

        private void UpdateGuidePosition()
        {
            if (_guideLine != null)
            {
                _guideLine.SetPosition(0, new Vector3(-PlatformXOffset, YPos, _guideZ));
                _guideLine.SetPosition(1, new Vector3(PlatformXOffset, YPos, _guideZ));
            }
            if (_guideLeftSphere != null)
                _guideLeftSphere.transform.position = new Vector3(-PlatformXOffset, YPos, _guideZ);
            if (_guideRightSphere != null)
                _guideRightSphere.transform.position = new Vector3(PlatformXOffset, YPos, _guideZ);
        }

        private void UpdateSideLightPositions()
        {
            float zStart = _guideZ - 1f;
            float zEnd = _guideZ + SpawnZDistance + 1f;

            UpdateSideLightLine(_leftSideLightObj, -PlatformXOffset, zStart, zEnd);
            UpdateSideLightLine(_rightSideLightObj, PlatformXOffset, zStart, zEnd);
        }

        private static void UpdateSideLightLine(GameObject? sideLightObj, float x, float zStart, float zEnd)
        {
            if (sideLightObj == null) return;
            var line = sideLightObj.GetComponent<LineRenderer>();
            if (line == null) return;
            line.SetPosition(0, new Vector3(x, YPos, zStart));
            line.SetPosition(1, new Vector3(x, YPos, zEnd));
        }

        private void ShiftMarkersZ(float delta)
        {
            foreach (var marker in _markers)
            {
                if (marker.LeftSphere != null)
                {
                    var pos = marker.LeftSphere.transform.position;
                    marker.LeftSphere.transform.position = new Vector3(pos.x, pos.y, pos.z + delta);
                }
                if (marker.RightSphere != null)
                {
                    var pos = marker.RightSphere.transform.position;
                    marker.RightSphere.transform.position = new Vector3(pos.x, pos.y, pos.z + delta);
                }
                if (marker.Line != null)
                {
                    var p0 = marker.Line.GetPosition(0);
                    var p1 = marker.Line.GetPosition(1);
                    marker.Line.SetPosition(0, new Vector3(p0.x, p0.y, p0.z + delta));
                    marker.Line.SetPosition(1, new Vector3(p1.x, p1.y, p1.z + delta));
                }
            }
        }

        public void UpdateColors()
        {
            _colorIndex = 0;

            // Recolor existing non-flashing markers
            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker.IsFlashing || marker.Container == null) continue;

                int colorMode = PluginConfig.Instance.ColorMode > 0 ? PluginConfig.Instance.ColorMode : 1;
                Color newColor = GetColorForIndex(i % colorMode);
                marker.Color = newColor;

                float alpha = PluginConfig.Instance.MarkerOpacity;
                Color colorWithAlpha = new Color(newColor.r, newColor.g, newColor.b, alpha);

                if (marker.LeftRenderer != null)
                    marker.LeftRenderer.material.color = colorWithAlpha;
                if (marker.RightRenderer != null)
                    marker.RightRenderer.material.color = colorWithAlpha;
                if (marker.Line != null)
                {
                    marker.Line.startColor = colorWithAlpha;
                    marker.Line.endColor = colorWithAlpha;
                }
                if (marker.LeftLight != null)
                    marker.LeftLight.color = newColor;
                if (marker.RightLight != null)
                    marker.RightLight.color = newColor;
            }
        }

        public void UpdateBeatDivision()
        {
            // Reset spawn timer so the new interval takes effect immediately
            _spawnTimer = 0f;
        }

        public void Dispose()
        {
            Hide();
        }
    }
}
