
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Custom editor for CoreMovement that allows dynamic management
    /// of IMovementAbility components.
    /// </summary>
    [CustomEditor(typeof(CoreMovement))]
    [CanEditMultipleObjects]
    public class CoreMovementEditor : Editor
    {
        private string[] m_AbilityNames;
        private GUIStyle m_HeaderStyle;
        private int m_SelectedAbilityIndex;
        private List<Type> m_MovementAbilityTypes;

        private const float k_AddButtonSpacing = 60f;
        private const float k_AddedAbilityLabelWidth = 180f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitializeStyles();

            EditorGUILayout.LabelField(
                "Movement Abilities",
                m_HeaderStyle
            );

            EditorGUILayout.BeginVertical();
            DrawCurrentAbilitiesSection();
            EditorGUILayout.Space(5);
            DrawAddAbilitySection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script"
            );

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            FindAllAbilityImplementations();
            PrepareDropdownArrays();
        }

        private void InitializeStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    margin = { top = 10, bottom = 10 }
                };
            }
        }

        private void FindAllAbilityImplementations()
        {
            m_MovementAbilityTypes =
                TypeCache.GetTypesDerivedFrom<IMovementAbility>()
                    .Where(t =>
                        !t.IsInterface &&
                        !t.IsAbstract &&
                        typeof(MonoBehaviour).IsAssignableFrom(t))
                    .ToList();
        }

        private void PrepareDropdownArrays()
        {
            if (m_MovementAbilityTypes != null &&
                m_MovementAbilityTypes.Count > 0)
            {
                m_AbilityNames = m_MovementAbilityTypes
                    .Select(t => t.Name)
                    .ToArray();
            }
            else
            {
                m_AbilityNames = new[] { "No abilities found" };
            }

            m_SelectedAbilityIndex = 0;
        }

        private void DrawCurrentAbilitiesSection()
        {
            CoreMovement coreMovement = (CoreMovement)target;
            var currentAbilities =
                coreMovement.GetComponents<IMovementAbility>();

            if (currentAbilities.Length == 0)
            {
                EditorGUILayout.LabelField(
                    "  No abilities added.",
                    EditorStyles.miniLabel
                );
                return;
            }

            foreach (var ability in currentAbilities)
            {
                var abilityComponent = ability as MonoBehaviour;
                if (abilityComponent == null)
                    continue;

                Rect controlRect = EditorGUILayout.GetControlRect(
                    false,
                    EditorGUIUtility.singleLineHeight
                );

                float buttonWidth = 60f;

                Rect buttonRect = new Rect(
                    controlRect.x + controlRect.width - buttonWidth,
                    controlRect.y,
                    buttonWidth,
                    controlRect.height
                );

                Rect labelRect = new Rect(
                    controlRect.x,
                    controlRect.y,
                    k_AddedAbilityLabelWidth,
                    controlRect.height
                );

                EditorGUI.LabelField(
                    labelRect,
                    abilityComponent.GetType().Name
                );

                if (GUI.Button(buttonRect, "Remove"))
                {
                    Undo.DestroyObjectImmediate(abilityComponent);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawAddAbilitySection()
        {
            CoreMovement coreMovement = (CoreMovement)target;

            if (m_AbilityNames == null ||
                m_MovementAbilityTypes == null ||
                m_MovementAbilityTypes.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "  No abilities found in project.",
                    EditorStyles.miniLabel
                );
                return;
            }

            Rect controlRect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight
            );

            float buttonWidth = 45f;

            Rect buttonRect = new Rect(
                controlRect.x + controlRect.width - buttonWidth,
                controlRect.y,
                buttonWidth,
                controlRect.height
            );

            float popupWidth =
                controlRect.width - buttonWidth - k_AddButtonSpacing;

            Rect popupRect = new Rect(
                controlRect.x,
                controlRect.y,
                popupWidth,
                controlRect.height
            );

            m_SelectedAbilityIndex = EditorGUI.Popup(
                popupRect,
                m_SelectedAbilityIndex,
                m_AbilityNames
            );

            Type selectedType =
                m_MovementAbilityTypes[m_SelectedAbilityIndex];

            bool alreadyExists =
                coreMovement.GetComponent(selectedType) != null;

            using (new EditorGUI.DisabledScope(alreadyExists))
            {
                if (GUI.Button(buttonRect, "Add"))
                {
                    Undo.AddComponent(
                        coreMovement.gameObject,
                        selectedType
                    );
                }
            }
        }
    }
}
