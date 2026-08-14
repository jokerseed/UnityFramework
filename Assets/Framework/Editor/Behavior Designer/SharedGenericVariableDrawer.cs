using System;
using System.Collections.Generic;
using BehaviorDesigner.Editor;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;

[CustomObjectDrawer(typeof(GenericVariable))]
public class SharedGenericVariableDrawer : ObjectDrawer
{
	private static string[] variableNames;

	public override void OnGUI(GUIContent label)
	{
		GenericVariable genericVariable = value as GenericVariable;
		EditorGUILayout.BeginVertical();
		if (FieldInspector.DrawFoldout(genericVariable.GetHashCode(), label))
		{
			EditorGUI.indentLevel++;
			if (variableNames == null)
			{
				List<Type> list = VariableInspector.FindAllSharedVariableTypes(removeShared: true);
				variableNames = new string[list.Count];
				for (int i = 0; i < list.Count; i++)
				{
					variableNames[i] = list[i].Name.Remove(0, 6);
				}
			}
			int num = 0;
			string text = genericVariable.type.Remove(0, 6);
			for (int j = 0; j < variableNames.Length; j++)
			{
				if (variableNames[j].Equals(text))
				{
					num = j;
					break;
				}
			}
			int num2 = EditorGUILayout.Popup("Type", num, variableNames, BehaviorDesignerUtility.SharedVariableToolbarPopup);
			Type type = VariableInspector.FindAllSharedVariableTypes(removeShared: true)[num2];
			if (num2 != num)
			{
				num = num2;
				genericVariable.value = Activator.CreateInstance(type) as SharedVariable;
			}
			GUILayout.Space(3f);
			genericVariable.type = "Shared" + variableNames[num];
			genericVariable.value = FieldInspector.DrawSharedVariable(null, new GUIContent("Value"), null, type, genericVariable.value);
			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndVertical();
	}
}
