using RoR2;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Drawers
{

    [CustomPropertyDrawer(typeof(EnumMaskAttribute))]
	public class EnumFlagDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EnumMaskAttribute flagSettings = (EnumMaskAttribute)attribute;
			Enum targetEnum = GetBaseProperty<Enum>(property);

			EditorGUI.BeginChangeCheck();
			EditorGUI.BeginProperty(position, label, property);

			Enum enumNew = EditorGUI.EnumFlagsField(position, label, targetEnum);

			EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
				try { property.intValue = (int)Convert.ChangeType(enumNew, targetEnum.GetType()); } catch { }
				try { property.intValue = (byte)Convert.ChangeType(enumNew, targetEnum.GetType()); } catch { }
				property.serializedObject.ApplyModifiedProperties();
				property.serializedObject.UpdateIfRequiredOrScript();
			}
		}

		static T GetBaseProperty<T>(SerializedProperty prop)
		{
			// Separate the steps it takes to get to this property
			string[] separatedPaths = prop.propertyPath.Split('.');

			// Go down to the root of this serialized property
			System.Object reflectionTarget = prop.serializedObject.targetObject as object;
			// Walk down the path to get the target object
			foreach (var path in separatedPaths)
			{
				FieldInfo fieldInfo = reflectionTarget.GetType().GetField(path);
				reflectionTarget = fieldInfo.GetValue(reflectionTarget);
			}
			return (T)reflectionTarget;
		}
	}
}