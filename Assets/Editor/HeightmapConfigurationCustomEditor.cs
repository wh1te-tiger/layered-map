using UnityEditor;
using UnityEngine;
using WorldGeneration.Heightmap;

namespace Editor
{
#if UNITY_EDITOR

    [CustomEditor(typeof(HeightmapConfiguration), true)]
    public class HeightmapConfigurationCustomEditor : UnityEditor.Editor
    {
        private const int PreviewSize = 256;
        private static GUIStyle _previewStyle;
        private HeightmapConfiguration _config;

        private void OnEnable()
        {
            _config = target as HeightmapConfiguration;
            Debug.Assert(_config != null, nameof(_config) + " != null");
            _config.ForceRefresh(); 
        }

        public override void OnInspectorGUI()
        {
            // Отрисовка стандартных свойств
            DrawDefaultInspector();

            _previewStyle ??= new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageOnly
            };

            // Автоматическое обновление при изменении
            if (GUI.changed)
            {
                _config.ForceRefresh();
                Repaint();
            }

            // Всегда вызываем GetRect, чтобы избежать рассинхронизации
            Rect rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize);

            // Отрисовка текстуру только при событии Repaint
            if (Event.current.type == EventType.Repaint)
            {
                Texture2D previewTexture = _config.GetHeightMapTexture();
                if (previewTexture != null)
                {
                    EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
                }
            }

            // Кнопка принудительного обновления
            if (GUILayout.Button("Force Refresh"))
            {
                _config.ForceRefresh();
                SceneView.RepaintAll();
            }
        }

        // Улучшение: перерисовка при активном выборе
        public override bool RequiresConstantRepaint()
        {
            return GUI.changed ||
                   EditorWindow.focusedWindow != null &&
                   EditorWindow.focusedWindow.titleContent.text.Contains("Inspector");
        }

    }
    #endif
}