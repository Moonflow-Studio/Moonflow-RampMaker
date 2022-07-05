using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Moonflow
{
    public class MFRampMaker: EditorWindow
    {
        public static MFRampMaker Ins;
        public bool isShow = false;
        public bool autoLinkMode = false;
        public Material targetMaterial;
        public string propertyName;

        public bool lerpMode;
        private bool _lerpMode;
        public int ribbonNum;
        private List<string> texNames;
        private int targetPropertySerial;
        private List<Gradient> _ribbons;
        // private Gradient _top;
        // private Gradient _bottom;
        private int _level;
        private int _size;
        private RenderTexture _rt;
        // private CommandBuffer _cmd;
        private Material _previewMat;
        private Color[] _tempColor;
        private float[] _tempPoint;
        private Texture2D _previewTex;
        private bool _isLinked = false;

        private static readonly int COLOR_ARRAY = Shader.PropertyToID("_ColorArray");
        private static readonly int POINT_ARRAY = Shader.PropertyToID("_PointArray");
        private static readonly int REAL_NUM = Shader.PropertyToID("_RealNum");
        private static readonly string LERP_MODE = "_LERP_MODE";

        [MenuItem("Moonflow/Tools/Art/MFRampMaker")]
        public static void ShowWindow()
        {
            Ins = GetWindow<MFRampMaker>();
            Ins.minSize = new Vector2(200, 200);
            Ins.maxSize = new Vector2(400, 300);
            Ins.InitData();
            Ins.Show();
        }
        [MenuItem("CONTEXT/Material/LinkToRampMaker", priority = 100)]
        public static void ShowWindow(MenuCommand menuCommand)
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
        

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUIUtility.labelWidth = 50;
                        EditorGUIUtility.fieldWidth = 50;
                        EditorGUILayout.PrefixLabel("参数");
                        lerpMode = EditorGUILayout.ToggleLeft("渐变模式", lerpMode);
                        if (_lerpMode != lerpMode)
                        {
                            _lerpMode = lerpMode;
                            if (_lerpMode)
                            {
                                Shader.EnableKeyword(LERP_MODE);
                            }
                            else
                            {
                                Shader.DisableKeyword(LERP_MODE);
                            }
                        }

                        ribbonNum = EditorGUILayout.IntSlider("条带数量", ribbonNum, 1, 8);
                        if (_ribbons.Count != ribbonNum)
                        {
                            UpdateRibbonNum();
                        }

                        for (int i = 0; i < _ribbons.Count; i++)
                        {
                            _ribbons[i] = EditorGUILayout.GradientField((i + 1).ToString(), _ribbons[i]);
                        }
                        if (GUILayout.Button("读取配置"))
                        {
                            string path = EditorUtility.OpenFilePanel("读取", Application.dataPath, "asset");
                            ReadConfig(path);
                        }
                        if (GUILayout.Button("保存配置"))
                        {
                            string path = EditorUtility.SaveFilePanel("保存到", Application.dataPath, "GradientConfig", "asset");
                            SaveConfig(path);
                        }
                    }
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        if (targetMaterial != null)
                        {
                            EditorGUILayout.ObjectField(targetMaterial, typeof(Material));
                            if (!autoLinkMode)
                            {
                                targetPropertySerial = EditorGUILayout.Popup("目标属性", targetPropertySerial, texNames.ToArray());
                                propertyName = texNames[targetPropertySerial];
                            }
                            else
                            {
                                EditorGUILayout.LabelField("目标属性", propertyName);
                            }
                            if (GUILayout.Button(_isLinked ? "断开链接" : "链接"))
                            {
                                if (_isLinked) DestroyLink();
                                else _isLinked = true;
                            }
                        }
                    }
                    
                }
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.PrefixLabel("贴图预览设置");
                    _level = EditorGUILayout.IntSlider("贴图分辨率级别", _level, 0, 4);
                    EditorGUILayout.LabelField("当前分辨率", Mathf.Pow(2, 5+_level).ToString(CultureInfo.CurrentCulture));

                    if (_rt!=null && _rt.IsCreated())
                    {
                        Rect rect = EditorGUILayout.GetControlRect(true, 200);
                        rect.width = 200;
                        EditorGUI.DrawPreviewTexture(rect, _rt);
                    }
                    if (GUILayout.Button("保存贴图"))
                    {
                        string path = EditorUtility.SaveFilePanel("保存到", Application.dataPath, "RampTex", "TGA");
                        SaveTex(path);
                    }
                }
            }
            
            
            if (EditorGUI.EndChangeCheck())
            {
                if (_size != (int)Mathf.Pow(2, 5 + _level))
                {
                    ReNewRT();
                }
                SetGradient();
            }
        }

        private void UpdateRibbonNum()
        {
            while (_ribbons.Count > ribbonNum)
            {
                _ribbons.RemoveAt(_ribbons.Count-1);
            }

            while (_ribbons.Count < ribbonNum)
            {
                _ribbons.Add(new Gradient());
            }
        }
        public void SaveTex(string path)
        {
            RenderTexture.active = _rt;
            _previewTex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
            _previewTex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            RenderTexture.active = null;
            byte[] bytes = _previewTex.EncodeToTGA();
            string[] ts = System.DateTime.Now.ToString().Split(' ', ':', '/');
            string time = string.Concat(ts);
            string filePath = "/TextureFromGradient " + time + ".TGA";
            string fileFullPath = Application.dataPath + filePath;
            string realPath = string.IsNullOrEmpty(path) ? fileFullPath : path;
            File.WriteAllBytes(realPath, bytes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (_isLinked)
            {
                if (realPath.StartsWith(Application.dataPath)) {
                    realPath = "Assets" + realPath.Substring(Application.dataPath.Length);
                }
                Texture2D savedTex = AssetDatabase.LoadAssetAtPath(realPath, typeof(Texture2D)) as Texture2D;
                targetMaterial.SetTexture(propertyName, savedTex);
                _isLinked = false;
            }
        }

        public void SaveConfig(string path)
        {
            MFMultiGradientAsset asset = ScriptableObject.CreateInstance<MFMultiGradientAsset>();
            Gradient[] temp = new Gradient[_ribbons.Count];
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = new Gradient();
                temp[i].SetKeys(_ribbons[i].colorKeys, _ribbons[i].alphaKeys);
            }
            asset.multiGradients = temp;
            AssetDatabase.CreateAsset(asset, "Assets" + path.Substring(Application.dataPath.Length));
        }
        public void ReadConfig(string path)
        {
            MFMultiGradientAsset asset = AssetDatabase.LoadAssetAtPath("Assets" + path.Substring(Application.dataPath.Length), typeof(MFMultiGradientAsset)) as MFMultiGradientAsset;
            ribbonNum = asset.multiGradients.Length ;
            _ribbons = new List<Gradient>(asset.multiGradients.ToArray());
        }

        private void OnInspectorUpdate()
        {
            if (_rt != null)
            {
                UpdateRT();
            }

            if (_isLinked)
            {
                targetMaterial.SetTexture(propertyName, _rt);
            }
        }

        private void UpdateProperty()
        {
            if (targetMaterial == null) return;
            targetMaterial.GetTexturePropertyNames(texNames);
        }

        private void DestroyLink()
        {
            _isLinked = false;
            if(!string.IsNullOrEmpty(propertyName))
                targetMaterial.SetTexture(propertyName, Texture2D.whiteTexture);
        }
        public void InitData()
        {
            isShow = true;
            _ribbons = new List<Gradient>();
            _ribbons.Add(new Gradient());
            texNames = new List<string>();
            // _cmd = new CommandBuffer();
            Shader s = Shader.Find("Hidden/Moonflow/RampMaker");
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
            if(_rt!=null && _rt.IsCreated()) _rt.Release();
        }

        private void NewRT()
        {
            ReleaseOldRT();
            _size = (int)Mathf.Pow(2, 5 + _level);
            _rt = new RenderTexture(_size, _size, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
            _rt.name = "preview";
            _rt.enableRandomWrite = true;
            _rt.Create();
        }

        private void UpdateRT()
        {
            if (_rt.IsCreated())
            {
                Graphics.Blit(Texture2D.whiteTexture, _rt, _previewMat);
            }
        }

        private void SetGradient()
        {
            _tempColor = new Color[80];
            _tempPoint = new float[80];
            for (int i = 0; i < _ribbons.Count; i++)
            {
                SetGradientToArray(_ribbons[i], i);
            }
            _previewMat.SetFloat(REAL_NUM, _ribbons.Count);
            _previewMat.SetColorArray(COLOR_ARRAY, _tempColor);
            _previewMat.SetFloatArray(POINT_ARRAY, _tempPoint);
        }

        private void SetGradientToArray(Gradient source, int serial)
        {
            int count = source.colorKeys.Length;
            int offset = 0;
            int outsideOffset = serial * 10;
            for (int i = 0; i < 10; i++)
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

        private void OnDisable()
        {
            DestroyLink();
            isShow = false;
            RenderTexture.active = null;
            _rt.Release();
            DestroyImmediate(_previewMat);
        }
    }
}