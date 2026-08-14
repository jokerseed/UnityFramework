using UnityEngine;

namespace BehaviorDesigner.Editor
{
    public class EditorZoomArea
    {
        private static Matrix4x4 _prevGuiMatrix;

        private static Rect groupRect = default(Rect);

        public static Rect Begin(Rect screenCoordsArea, float zoomScale)
        {
            GUI.EndGroup();
            Rect rect = screenCoordsArea.ScaleSizeBy(1f / zoomScale, screenCoordsArea.TopLeft());
            rect.y += 21f;
            GUI.BeginGroup(rect);
            _prevGuiMatrix = GUI.matrix;
            Matrix4x4 matrix4x = Matrix4x4.TRS(rect.TopLeft(), Quaternion.identity, Vector3.one);
            Vector3 one = Vector3.one;
            one.x = (one.y = zoomScale);
            Matrix4x4 matrix4x2 = Matrix4x4.Scale(one);
            GUI.matrix = matrix4x * matrix4x2 * matrix4x.inverse * GUI.matrix;
            return rect;
        }

        public static void End()
        {
            GUI.matrix = _prevGuiMatrix;
            GUI.EndGroup();
            groupRect.y = 21f;
            groupRect.width = Screen.width;
            groupRect.height = Screen.height;
            GUI.BeginGroup(groupRect);
        }
    }
}