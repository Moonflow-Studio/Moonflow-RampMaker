using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Moonflow
{
    public class MFRampMaker : EditorWindow
    {
        public static MFRampMaker Ins;

        private static readonly int COLOR_ARRAY = Shader.PropertyToID("_ColorArray");
        private static readonly int POINT_ARRAY = Shader.PropertyToID("_PointArray");
        private static readonly int REAL_NUM = Shader.PropertyToID("_RealNum");
        private static readonly string LERP_MODE = "_LERP_MODE";
        private static readonly string GAMMA_MODE = "_GAMMA_MODE";
        private static readonly string QUAD_MODE = "_QUAD_MODE";
        private static readonly string LOOP_MODE = "_LOOP_MODE";
        public bool isShow;
        public bool autoLinkMode;
        public Renderer targetRenderer;
        public Material targetMaterial;
        public string propertyName;

        public bool lerpMode;
        public bool gammaMode;
        public bool quadMode;
        public bool loopMode;
        public int ribbonNum;
        private bool _gammaMode;
        private bool _isLinked;

        private bool _lerpMode;

        // private Gradient _top;
        // private Gradient _bottom;
        private int _level;
        private bool _loopMode;

        private Texture2D _oldTex;

        // private CommandBuffer _cmd;
        private Material _previewMat;
        private Texture2D _previewTex;
        private bool _quadMode;
        private List<Gradient> _ribbons;
        private RenderTexture _rt;
        private int _size;
        private Color[] _tempColor;
        private float[] _tempPoint;
        private int targetPropertySerial;
        private List<string> texNames;

        private void OnEnable()
        {
            // 反序列化恢复路径下 private 字段不保留，必须在这里初始化
            // 避免 Unity 重启后 OnGUI 第一次访问 _ribbons 时 NRE
            EnsureInit();
        }

        private void OnDisable()
        {
            DestroyLink();
            isShow = false;
            RenderTexture.active = null;
            if (_rt != null)
            {
                if (_rt.IsCreated()) _rt.Release();
                _rt = null;
            }
            if (_previewMat != null)
            {
                DestroyImmediate(_previewMat);
                _previewMat = null;
            }
        }

        /// <summary>
        /// 幂等初始化。保证 _ribbons / texNames / _previewMat / _rt 至少存在有效实例。
        /// 重复调用不会泄漏旧资源（_rt / _previewMat 走 Release/Destroy 后重建）。
        /// </summary>
        private void EnsureInit()
        {
            isShow = true;
            if (_ribbons == null || _ribbons.Count == 0)
            {
                _ribbons = new List<Gradient> { CreateDefaultGradient() };
                ribbonNum = Mathf.Max(1, ribbonNum);
            }
            if (texNames == null) texNames = new List<string>();

            if (_previewMat == null)
            {
                var s = Shader.Find("Hidden/Moonflow/RampMaker");
                if (s == null)
                {
                    Debug.LogError("[MFRampMaker] Hidden/Moonflow/RampMaker shader 未找到。检查 shader 是否在项目内。");
                    return;
                }
                _previewMat = new Material(s);
            }

            if (_rt == null) NewRT();
            SetGradient();
        }

        private static Gradient CreateDefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }


        private void OnGUI()
        {
            // 双保险：极端情况下（如 OnEnable 抛异常）确保数据有效
            EnsureInit();

            var changeTexSize = false;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUIUtility.labelWidth = 100;
                        EditorGUIUtility.fieldWidth = 50;
                        EditorGUILayout.PrefixLabel(MFToolsLang.isCN?"参数":"Properties");
                        lerpMode = EditorGUILayout.ToggleLeft(MFToolsLang.isCN?"条带插值":"Lerp Ribbon", lerpMode);
                        gammaMode = EditorGUILayout.ToggleLeft(MFToolsLang.isCN?"Gamma颜色":"Gamma Color", gammaMode);
                        quadMode = EditorGUILayout.ToggleLeft(MFToolsLang.isCN?"方形贴图":"Quad Tex", quadMode);
                        loopMode = EditorGUILayout.ToggleLeft(MFToolsLang.isCN?"纵轴":"Loop Vertical", loopMode);
                        if (_previewMat != null)
                        {
                            if (_lerpMode != lerpMode)
                            {
                                _lerpMode = lerpMode;
                                if (_lerpMode)
                                    _previewMat.EnableKeyword(LERP_MODE);
                                else
                                    _previewMat.DisableKeyword(LERP_MODE);
                            }

                            if (_gammaMode != gammaMode)
                            {
                                _gammaMode = gammaMode;
                                if (_gammaMode)
                                    _previewMat.EnableKeyword(GAMMA_MODE);
                                else
                                    _previewMat.DisableKeyword(GAMMA_MODE);
                            }

                            if (_quadMode != quadMode)
                            {
                                _quadMode = quadMode;
                                if (_quadMode)
                                    _previewMat.EnableKeyword(QUAD_MODE);
                                else
                                    _previewMat.DisableKeyword(QUAD_MODE);
                                changeTexSize = true;
                            }

                            if (_loopMode != loopMode)
                            {
                                _loopMode = loopMode;
                                if (_loopMode)
                                    _previewMat.EnableKeyword(LOOP_MODE);
                                else
                                    _previewMat.DisableKeyword(LOOP_MODE);
                            }
                        }

                        ribbonNum = EditorGUILayout.IntSlider(MFToolsLang.isCN?"条带数量":"Ribbon Num", ribbonNum, 1, 8);
                        if (_ribbons.Count != ribbonNum) UpdateRibbonNum();

                        for (var i = 0; i < _ribbons.Count; i++)
                            _ribbons[i] = EditorGUILayout.GradientField((i + 1).ToString(), _ribbons[i]);
                        if (GUILayout.Button(MFToolsLang.isCN?"读配置":"Read Config"))
                        {
                            var path = EditorUtility.OpenFilePanel("Read", Application.dataPath, "asset");
                            ReadConfig(path);
                        }

                        if (GUILayout.Button(MFToolsLang.isCN?"保存配置":"Save Config"))
                        {
                            var path = EditorUtility.SaveFilePanel("Save As", Application.dataPath, "GradientConfig",
                                "asset");
                            SaveConfig(path);
                        }
                    }

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        // if (ReferenceEquals(targetMaterial, null))
                        // {
                        EditorGUILayout.ObjectField(targetMaterial, typeof(Material), false);
                        if (!autoLinkMode && !ReferenceEquals(targetMaterial, null))
                        {
                            // 防御：texNames 为空时（材质未刷新属性）跳过 Popup，避免越界
                            if (texNames == null || texNames.Count == 0)
                            {
                                EditorGUILayout.LabelField(MFToolsLang.isCN?"目标参数":"Target Property",
                                    MFToolsLang.isCN?"<无贴图属性>":"<No Tex Property>");
                            }
                            else
                            {
                                targetPropertySerial = Mathf.Clamp(targetPropertySerial, 0, texNames.Count - 1);
                                targetPropertySerial = EditorGUILayout.Popup(MFToolsLang.isCN?"目标参数":"Target property", targetPropertySerial,
                                    texNames.ToArray());
                                propertyName = texNames[targetPropertySerial];
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField(MFToolsLang.isCN?"目标参数":"Target Property", propertyName);
                        }

                        if (GUILayout.Button(_isLinked ? (MFToolsLang.isCN?"断开链接":"Break Link") : (MFToolsLang.isCN?"链接到目标参数":"Link to target property")))
                        {
                            if (_isLinked) DestroyLink();
                            else
                                StartLink();
                        }
                        // }
                    }
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUIUtility.labelWidth = 120;
                    EditorGUIUtility.fieldWidth = 50;
                    EditorGUILayout.PrefixLabel(MFToolsLang.isCN?"贴图预览设置":"Texture Preview Settings");
                    _level = EditorGUILayout.IntSlider(MFToolsLang.isCN?"分辨率预览级别":"Resolution Preview Level", _level, 0, 4);
                    EditorGUILayout.LabelField(MFToolsLang.isCN?"当前尺寸（像素）":"Current Size(pixels)",
                        Mathf.Pow(2, 5 + _level).ToString(CultureInfo.CurrentCulture));

                    if (_rt != null && _rt.IsCreated())
                    {
                        var rect = EditorGUILayout.GetControlRect(true, 200);
                        rect.width = 200;
                        EditorGUI.DrawPreviewTexture(rect, _rt);
                    }

                    if (GUILayout.Button("Save Texture As"))
                    {
                        var path = EditorUtility.SaveFilePanel("Save to", Application.dataPath, "RampTex", "TGA");
                        SaveTex(path);
                    }
                }
            }


            if (EditorGUI.EndChangeCheck())
            {
                if (_size != (int)Mathf.Pow(2, 5 + _level) || changeTexSize) ReNewRT();
                SetGradient();
            }
        }

        private void OnInspectorUpdate()
        {
            if (_rt != null) UpdateRT();

            // 防御：targetMaterial 可能被用户在外部销毁；_rt 也可能未初始化
            if (_isLinked && targetMaterial != null && !targetMaterial.Equals(null) && _rt != null && !string.IsNullOrEmpty(propertyName))
                targetMaterial.SetTexture(propertyName, _rt);
        }

        [MenuItem("Tools/Moonflow/Tools/Art/RampMaker &#T")]
        public static void ShowWindow()
        {
            Ins = GetWindow<MFRampMaker>();
            Ins.minSize = new Vector2(500, 300);
            // Ins.maxSize = new Vector2(400, 300);
            Ins.EnsureInit();
            Ins.Show();
        }

        [MenuItem("CONTEXT/Material/LinkToRampMaker", priority = 100)]
        public static void MatLinkWindow(MenuCommand menuCommand)
        {
            if (Ins == null)
            {
                Ins = GetWindow<MFRampMaker>();
                Ins.EnsureInit();
                Ins.Show();
            }
            Ins.targetMaterial = menuCommand.context as Material;
            Ins.UpdateProperty();
        }

        //预留给MaterialPropertyDrawer的链接
        public static void ShowWindow(Material mat, string propertyName)
        {
            if (Ins == null)
            {
                Ins = GetWindow<MFRampMaker>();
                Ins.EnsureInit();
                Ins.Show();
            }

            Ins.targetMaterial = mat;
            Ins.autoLinkMode = true;
            Ins.propertyName = propertyName;
            Ins._isLinked = true;
            Ins.UpdateProperty();
        }

        private void StartLink()
        {
            // 防御：未选 material / property 时拒绝链接，避免后续 NRE
            if (targetMaterial == null || targetMaterial.Equals(null))
            {
                Debug.LogWarning("[MFRampMaker] 未指定 Target Material，无法链接。");
                return;
            }
            if (string.IsNullOrEmpty(propertyName))
            {
                Debug.LogWarning("[MFRampMaker] 未指定 Target Property，无法链接。");
                return;
            }
            _isLinked = true;
            _oldTex = targetMaterial.GetTexture(propertyName) as Texture2D;
        }

        private void UpdateRibbonNum()
        {
            // 防御：ribbonNum 下限 1（与 IntSlider 一致）
            ribbonNum = Mathf.Clamp(ribbonNum, 1, 8);

            while (_ribbons.Count > ribbonNum) _ribbons.RemoveAt(_ribbons.Count - 1);

            while (_ribbons.Count < ribbonNum)
            {
                var newGrad = new Gradient();
                if (_ribbons.Count > 0)
                {
                    // 复制上一条渐变作为起点
                    var last = _ribbons[^1];
                    newGrad.SetKeys(last.colorKeys, last.alphaKeys);
                }
                else
                {
                    // 首条：默认白色渐变
                    newGrad.SetKeys(
                        new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                    );
                }
                _ribbons.Add(newGrad);
            }
        }

        public void SaveTex(string path)
        {
            // 防御：_rt 未初始化时直接拒绝
            if (_rt == null)
            {
                Debug.LogWarning("[MFRampMaker] RenderTexture 未初始化，无法保存。");
                return;
            }
            RenderTexture.active = _rt;
            _previewTex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
            _previewTex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            _previewTex.wrapMode = TextureWrapMode.Clamp;
            RenderTexture.active = null;
            var bytes = _previewTex.EncodeToTGA();
            var ts = DateTime.Now.ToString().Split(' ', ':', '/');
            var time = string.Concat(ts);
            var filePath = "/TextureFromGradient " + time + ".TGA";
            var fileFullPath = Application.dataPath + filePath;
            var realPath = string.IsNullOrEmpty(path) ? fileFullPath : path;
            File.WriteAllBytes(realPath, bytes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (_isLinked)
            {
                if (realPath.StartsWith(Application.dataPath))
                    realPath = "Assets" + realPath.Substring(Application.dataPath.Length);
                var savedTex = AssetDatabase.LoadAssetAtPath(realPath, typeof(Texture2D)) as Texture2D;
                if (savedTex != null)
                {
                    savedTex.wrapMode = TextureWrapMode.Clamp;
                    AssetDatabase.SaveAssets();
                    // 防御：targetMaterial 可能在链接期间被外部销毁
                    if (targetMaterial != null && !targetMaterial.Equals(null) && !string.IsNullOrEmpty(propertyName))
                        targetMaterial.SetTexture(propertyName, savedTex);
                }

                _oldTex = savedTex;
                _isLinked = false;
            }
        }

        public void SaveConfig(string path)
        {
            // 防御：用户在 SaveFilePanel 中点击取消
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Application.dataPath))
            {
                Debug.LogWarning("[MFRampMaker] SaveConfig 取消或路径非法，未保存。");
                return;
            }
            var asset = CreateInstance<MFMultiGradientAsset>();
            var temp = new Gradient[_ribbons.Count];
            for (var i = 0; i < temp.Length; i++)
            {
                temp[i] = new Gradient();
                temp[i].SetKeys(_ribbons[i].colorKeys, _ribbons[i].alphaKeys);
            }

            asset.multiGradients = temp;
            AssetDatabase.CreateAsset(asset, "Assets" + path.Substring(Application.dataPath.Length));
        }

        public void ReadConfig(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Application.dataPath))
            {
                Debug.LogWarning("[MFRampMaker] ReadConfig 取消或路径非法，未读取。");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath("Assets" + path.Substring(Application.dataPath.Length),
                typeof(MFMultiGradientAsset)) as MFMultiGradientAsset;
            // 防御：路径错 / 文件非 MFMultiGradientAsset 时 asset 为 null
            if (asset == null || asset.multiGradients == null || asset.multiGradients.Length == 0)
            {
                Debug.LogWarning("[MFRampMaker] 配置文件无效或为空。");
                return;
            }
            ribbonNum = asset.multiGradients.Length;
            _ribbons = new List<Gradient>(asset.multiGradients.ToArray());
        }

        private void UpdateProperty()
        {
            if (targetMaterial == null) return;
            targetMaterial.GetTexturePropertyNames(texNames);
        }

        private void DestroyLink()
        {
            if (!_isLinked) return;
            _isLinked = false;
            // 防御：targetMaterial 可能被外部销毁（OnDisable 时尤其常见）
            if (targetMaterial != null && !targetMaterial.Equals(null) && !string.IsNullOrEmpty(propertyName))
            {
                targetMaterial.SetTexture(propertyName, _oldTex != null ? _oldTex : Texture2D.whiteTexture);
            }
            _oldTex = null;
        }

        public void InitData()
        {
            // 历史入口：保留为 EnsureInit 的别名，确保旧调用方仍可工作
            // 真正的初始化逻辑在 EnsureInit 中，幂等且 null-safe
            EnsureInit();
        }

        private void ReNewRT()
        {
            ReleaseOldRT();
            NewRT();
        }

        private void ReleaseOldRT()
        {
            if (_rt != null && _rt.IsCreated()) _rt.Release();
        }

        private void NewRT()
        {
            ReleaseOldRT();
            _size = (int)Mathf.Pow(2, 5 + _level);
            // 防御：_ribbons 为空时高度至少 2，避免 0 高度 RT
            var ribbonsCount = _ribbons != null ? Mathf.Max(1, _ribbons.Count) : 1;
            var height = _quadMode ? _size : ribbonsCount * 2;
            _rt = new RenderTexture(_size, height, 0, RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB);
            _rt.name = "preview";
            _rt.enableRandomWrite = true;
            _rt.Create();
        }

        private void UpdateRT()
        {
            if (_rt != null && _rt.IsCreated() && _previewMat != null)
                Graphics.Blit(Texture2D.whiteTexture, _rt, _previewMat);
        }

        private void SetGradient()
        {
            // 防御：_previewMat 可能为 null（shader 缺失时 EnsureInit 提前 return）
            if (_previewMat == null) return;
            // 防御：_ribbons 在极端情况下可能为 null
            if (_ribbons == null || _ribbons.Count == 0) return;

            _tempColor = new Color[80];
            _tempPoint = new float[80];
            for (var i = 0; i < _ribbons.Count; i++) SetGradientToArray(_ribbons[i], i);
            _previewMat.SetFloat(REAL_NUM, _ribbons.Count);
            _previewMat.SetColorArray(COLOR_ARRAY, _tempColor);
            _previewMat.SetFloatArray(POINT_ARRAY, _tempPoint);
        }

        private void SetGradientToArray(Gradient source, int serial)
        {
            // 防御：source 或 colorKeys 为空时跳过
            if (source == null || source.colorKeys == null || source.colorKeys.Length == 0) return;

            var count = source.colorKeys.Length;
            var offset = 0;
            var outsideOffset = serial * 10;
            for (var i = 0; i < 10; i++)
            {
                if (i == 0 && source.colorKeys[0].time != 0)
                {
                    _tempColor[outsideOffset] = source.colorKeys[0].color;
                    _tempPoint[outsideOffset] = 0;
                    offset = -1;
                    continue;
                }

                if (i + offset < count)
                {
                    _tempColor[outsideOffset + i] = source.colorKeys[i + offset].color;
                    _tempPoint[outsideOffset + i] = source.colorKeys[i + offset].time;
                }
                else
                {
                    _tempColor[outsideOffset + i] = source.colorKeys[count - 1].color;
                    _tempPoint[outsideOffset + i] = 1;
                }
            }
        }
    }
}