using System.Linq;
using Moonflow;
using UnityEditor;
using UnityEngine;

namespace MoonflowShading.Editor
{
    public class MFRampDrawer: MaterialPropertyDrawer
    {
        private Material _mat;
        private string[] _keywords;

        public MFRampDrawer()
        {
        }
        public MFRampDrawer(params string[] keywords)
        {
            _keywords = keywords;
        }

        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            _mat = editor.target as Material;
            if (_keywords != null)
            {
                if(_keywords.Any(t => _mat.IsKeywordEnabled(t)))
                    DrawGUI(prop, label, editor);
            }
            else
            {
                DrawGUI(prop, label, editor);
            }
        }

        private void DrawGUI(MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            EditorGUI.indentLevel++;
            editor.TexturePropertySingleLine(label, prop);
            if (GUILayout.Button("Make"))
            {
                MFRampMaker.ShowWindow(_mat, prop.name);
            }
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return 0f;
        }
    }
}