using System;
using UnityEditor;
using UnityEngine;

namespace BehaviorDesigner.Editor
{
    [Serializable]
    public class NodeConnection : ScriptableObject
    {
        [SerializeField]
        private NodeDesigner originatingNodeDesigner;

        [SerializeField]
        private NodeDesigner destinationNodeDesigner;

        [SerializeField]
        private NodeConnectionType nodeConnectionType;

        [SerializeField]
        private bool selected;

        [SerializeField]
        private float horizontalHeight;

        private readonly Color selectedDisabledProColor = new Color(0.1316f, 0.3212f, 0.4803f);

        private readonly Color selectedDisabledStandardColor = new Color(0.1701f, 0.3982f, 0.5873f);

        private readonly Color selectedEnabledProColor = new Color(0.188f, 0.4588f, 0.6862f);

        private readonly Color selectedEnabledStandardColor = new Color(0.243f, 0.5686f, 0.839f);

        private readonly Color taskRunningProColor = new Color(0f, 0.698f, 0.4f);

        private readonly Color taskRunningStandardColor = new Color(0f, 1f, 0.2784f);

        private bool horizontalDirty = true;

        private Vector2 startHorizontalBreak;

        private Vector2 endHorizontalBreak;

        private Vector3[] linePoints = new Vector3[4];

        public NodeDesigner OriginatingNodeDesigner
        {
            get
            {
                return originatingNodeDesigner;
            }
            set
            {
                originatingNodeDesigner = value;
            }
        }

        public NodeDesigner DestinationNodeDesigner
        {
            get
            {
                return destinationNodeDesigner;
            }
            set
            {
                destinationNodeDesigner = value;
            }
        }

        public NodeConnectionType NodeConnectionType
        {
            get
            {
                return nodeConnectionType;
            }
            set
            {
                nodeConnectionType = value;
            }
        }

        public float HorizontalHeight
        {
            set
            {
                horizontalHeight = value;
                horizontalDirty = true;
            }
        }

        public void select()
        {
            selected = true;
        }

        public void deselect()
        {
            selected = false;
        }

        public void OnEnable()
        {
            base.hideFlags = HideFlags.HideAndDontSave;
        }

        public void LoadConnection(NodeDesigner nodeDesigner, NodeConnectionType nodeConnectionType)
        {
            originatingNodeDesigner = nodeDesigner;
            this.nodeConnectionType = nodeConnectionType;
            selected = false;
        }

        public void DrawConnection(Vector2 offset, bool disabled)
        {
            DrawConnection(OriginatingNodeDesigner.GetConnectionPosition(offset, NodeConnectionType.Outgoing), DestinationNodeDesigner.GetConnectionPosition(offset, NodeConnectionType.Incoming), disabled);
        }

        public void DrawConnection(Vector2 source, Vector2 destination, bool disabled)
        {
            Color color = ((!disabled) ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            bool flag = destinationNodeDesigner != null && destinationNodeDesigner.Task != null && destinationNodeDesigner.Task.NodeData.PushTime != -1f && destinationNodeDesigner.Task.NodeData.PushTime >= destinationNodeDesigner.Task.NodeData.PopTime;
            float num = ((!BehaviorDesignerPreferences.GetBool(BDPreferences.FadeNodes)) ? 0.01f : 0.5f);
            if (selected)
            {
                color = (disabled ? ((!EditorGUIUtility.isProSkin) ? selectedDisabledStandardColor : selectedDisabledProColor) : ((!EditorGUIUtility.isProSkin) ? selectedEnabledStandardColor : selectedEnabledProColor));
            }
            else if (flag)
            {
                color = ((!EditorGUIUtility.isProSkin) ? taskRunningStandardColor : taskRunningProColor);
            }
            else if (num != 0f && destinationNodeDesigner != null && destinationNodeDesigner.Task != null && destinationNodeDesigner.Task.NodeData.PopTime != -1f && destinationNodeDesigner.Task.NodeData.PopTime <= Time.realtimeSinceStartup && Time.realtimeSinceStartup - destinationNodeDesigner.Task.NodeData.PopTime < num)
            {
                float t = 1f - (Time.realtimeSinceStartup - destinationNodeDesigner.Task.NodeData.PopTime) / num;
                Color white = Color.white;
                color = Color.Lerp(b: (!EditorGUIUtility.isProSkin) ? taskRunningStandardColor : taskRunningProColor, a: Color.white, t: t);
            }
            Handles.color = color;
            if (horizontalDirty)
            {
                startHorizontalBreak = new Vector2(source.x, horizontalHeight);
                endHorizontalBreak = new Vector2(destination.x, horizontalHeight);
                horizontalDirty = false;
            }
            ref Vector3 reference = ref linePoints[0];
            reference = source;
            ref Vector3 reference2 = ref linePoints[1];
            reference2 = startHorizontalBreak;
            ref Vector3 reference3 = ref linePoints[2];
            reference3 = endHorizontalBreak;
            ref Vector3 reference4 = ref linePoints[3];
            reference4 = destination;
            Handles.DrawPolyLine(linePoints);
            for (int i = 0; i < linePoints.Length; i++)
            {
                linePoints[i].x += 1f;
                linePoints[i].y += 1f;
            }
            Handles.DrawPolyLine(linePoints);
        }

        public bool Contains(Vector2 point, Vector2 offset)
        {
            Vector2 center = originatingNodeDesigner.OutgoingConnectionRect(offset).center;
            Vector2 vector = new Vector2(center.x, horizontalHeight);
            float num = Mathf.Abs(point.x - center.x);
            if (num < 7f && ((point.y >= center.y && point.y <= vector.y) || (point.y <= center.y && point.y >= vector.y)))
            {
                return true;
            }
            Rect rect = destinationNodeDesigner.IncomingConnectionRect(offset);
            Vector2 vector2 = new Vector2(rect.center.x, rect.y);
            Vector2 vector3 = new Vector2(vector2.x, horizontalHeight);
            num = Mathf.Abs(point.y - horizontalHeight);
            if (num < 7f && ((point.x <= center.x && point.x >= vector3.x) || (point.x >= center.x && point.x <= vector3.x)))
            {
                return true;
            }
            num = Mathf.Abs(point.x - vector2.x);
            if (num < 7f && ((point.y >= vector2.y && point.y <= vector3.y) || (point.y <= vector2.y && point.y >= vector3.y)))
            {
                return true;
            }
            return false;
        }
    }
}