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

        private void OnDisable()
        {
            DestroyLink();
            isShow = false;
            RenderTexture.active = null;
            _rt.Release();
            DestroyImmediate(_previewMat);
        }


        private void OnGUI()
        {
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
                            targetPropertySerial = EditorGUILayout.Popup(MFToolsLang.isCN?"目标参数":"Target property", targetPropertySerial,
                                texNames.ToArray());
                            propertyName = texNames[targetPropertySerial];
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

            if (_isLinked) targetMaterial.SetTexture(propertyName, _rt);
        }

        [MenuItem("Tools/Moonflow/Tools/Art/RampMaker &#T")]
        public static void ShowWindow()
        {
            Ins = GetWindow<MFRampMaker>();
            Ins.minSize = new Vector2(500, 300);
            // Ins.maxSize = new Vector2(400, 300);
            Ins.InitData();
            Ins.Show();
        }

        [MenuItem("CONTEXT/Material/LinkToRampMaker", priority = 100)]
        public static void MatLinkWindow(MenuCommand menuCommand)
        {
            if (Ins == null)
            {
                Ins = GetWindow<MFRampMaker>();
                Ins.InitData();
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
                Ins.InitData();
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
            _isLinked = true;
            _oldTex = targetMaterial.GetTexture(propertyName) as Texture2D;
        }

        private void UpdateRibbonNum()
        {
            while (_ribbons.Count > ribbonNum) _ribbons.RemoveAt(_ribbons.Count - 1);

            while (_ribbons.Count < ribbonNum)
            {
                var newGrad = new Gradient();
                var last = _ribbons[^1];
                newGrad.SetKeys(last.colorKeys, last.alphaKeys);
                _ribbons.Add(newGrad);
            }
        }

        public void SaveTex(string path)
        {
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
                    targetMaterial.SetTexture(propertyName, savedTex);
                }

                _oldTex = savedTex;
                _isLinked = false;
            }
        }

        public void SaveConfig(string path)
        {
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
            if (string.IsNullOrEmpty(path)) return;
            var asset = AssetDatabase.LoadAssetAtPath("Assets" + path.Substring(Application.dataPath.Length),
                typeof(MFMultiGradientAsset)) as MFMultiGradientAsset;
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
            _isLinked = false;
            if (!string.IsNullOrEmpty(propertyName))
                targetMaterial.SetTexture(propertyName, _oldTex != null ? _oldTex : Texture2D.whiteTexture);
        }

        public void InitData()
        {
            isShow = true;
            _ribbons = new List<Gradient>();
            _ribbons.Add(new Gradient());
            texNames = new List<string>();
            // _cmd = new CommandBuffer();
            var s = Shader.Find("Hidden/Moonflow/RampMaker");
            _previewMat = new Material(s);
            NewRT();
            SetGradient();
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
            _rt = new RenderTexture(_size, _quadMode ? _size : _ribbons.Count * 2, 0, RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB);
            _rt.name = "preview";
            _rt.enableRandomWrite = true;
            _rt.Create();
        }

        private void UpdateRT()
        {
            if (_rt.IsCreated()) Graphics.Blit(Texture2D.whiteTexture, _rt, _previewMat);
        }

        private void SetGradient()
        {
            _tempColor = new Color[80];
            _tempPoint = new float[80];
            for (var i = 0; i < _ribbons.Count; i++) SetGradientToArray(_ribbons[i], i);
            _previewMat.SetFloat(REAL_NUM, _ribbons.Count);
            _previewMat.SetColorArray(COLOR_ARRAY, _tempColor);
            _previewMat.SetFloatArray(POINT_ARRAY, _tempPoint);
        }

        private void SetGradientToArray(Gradient source, int serial)
        {
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