using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Unit
{
    public static class StateScriptValueExpressionDrawer
    {
        private static readonly ComparatorFactory s_expressionFactory = CreateExpressionFactory();

        public static void Draw(
            ValueExpression expression,
            UnitValueCategory expectedCategory,
            UnitSourceSchema sourceSchema,
            Action onChanged = null)
        {
            if (expression == null)
                return;

            EditorGUILayout.BeginVertical("box");
            expression.Kind = (ValueExpressionKind)EditorGUILayout.EnumPopup("Value Kind", expression.Kind);
            if (expression.Kind == ValueExpressionKind.Literal)
                DrawLiteral(expression, expectedCategory);
            else if (expression.Kind == ValueExpressionKind.Getter)
                DrawGetter(expression, expectedCategory, 0, sourceSchema, onChanged);
            else if (expression.Kind == ValueExpressionKind.Operation)
                DrawOperation(expression, expectedCategory, 0, sourceSchema, onChanged);

            EditorGUILayout.EndVertical();
        }

        public static UnitValue DrawLiteralValue(UnitValue value, UnitValueCategory expectedCategory)
        {
            ValueExpression expression = new()
            {
                Literal = value,
            };
            DrawLiteral(expression, expectedCategory);
            return expression.Literal;
        }

        public static void DrawCondition(
            ConditionConfig condition,
            UnitSourceSchema sourceSchema,
            Action onChanged = null)
        {
            if (condition == null)
                return;

            condition.ConditionType = ConditionType.Necessary;
            EditorGUILayout.LabelField("Condition", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DrawCompareInputs(condition, sourceSchema, onChanged);
            EditorGUILayout.EndVertical();
        }

        private static void DrawLiteral(ValueExpression expression, UnitValueCategory expectedCategory)
        {
            UnitValueCategory category = expectedCategory == UnitValueCategory.Any
                ? GetConcreteCategory(expression.Literal.Category)
                : expectedCategory;
            if (expectedCategory == UnitValueCategory.Any)
            {
                category = GetConcreteCategory((UnitValueCategory)EditorGUILayout.EnumPopup("Value Type", category));
            }

            if (expression.Literal.Category != category)
                expression.Literal = CreateDefaultLiteral(category);

            if (category == UnitValueCategory.Bool)
            {
                expression.Literal = UnitValue.FromBool(EditorGUILayout.Toggle("Value", expression.Literal.Bool));
            }
            else if (category == UnitValueCategory.Number)
            {
                if (!expression.Literal.TryGetNumber(out float number))
                    number = 0f;
                expression.Literal = UnitValue.FromFloat(EditorGUILayout.FloatField("Value", number));
            }
            else if (category == UnitValueCategory.Float2)
            {
                Vector2 value = new(expression.Literal.Float2.x, expression.Literal.Float2.y);
                value = EditorGUILayout.Vector2Field("Value", value);
                expression.Literal = UnitValue.FromFloat2(new float2(value.x, value.y));
            }
            else if (category == UnitValueCategory.Float3)
            {
                Vector3 value = new(expression.Literal.Float3.x, expression.Literal.Float3.y, expression.Literal.Float3.z);
                value = EditorGUILayout.Vector3Field("Value", value);
                expression.Literal = UnitValue.FromFloat3(new float3(value.x, value.y, value.z));
            }
            else if (category == UnitValueCategory.Entity)
            {
                int index = EditorGUILayout.IntField("Entity Index", expression.Literal.Entity.Index);
                int version = EditorGUILayout.IntField("Entity Version", expression.Literal.Entity.Version);
                expression.Literal = UnitValue.FromEntity(new Entity { Index = index, Version = version });
            }
            else if (category == UnitValueCategory.String)
            {
                expression.Literal = UnitValue.FromString(EditorGUILayout.TextField("Value", expression.Literal.String ?? string.Empty));
            }
        }

        private static void DrawGetter(
            ValueExpression expression,
            UnitValueCategory expectedCategory,
            int depth,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            if (depth >= 6)
            {
                EditorGUILayout.HelpBox("Expression nesting is limited to 6 levels.", MessageType.Warning);
                return;
            }

            List<UnitSourceGetSchemaEntry> entries = (sourceSchema?.Gets ?? Enumerable.Empty<UnitSourceGetSchemaEntry>())
                .Where(entry => expectedCategory == UnitValueCategory.Any || entry.ReturnType == expectedCategory)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox($"No getter returns {expectedCategory}.", MessageType.Warning);
                return;
            }

            StateScriptAccessorDropdown.Draw(
                "Getter",
                expression.GetterKey,
                entries.Select(entry => entry.Key),
                "(Select getter)",
                selectedKey =>
                {
                    if (string.Equals(expression.GetterKey, selectedKey, StringComparison.Ordinal))
                        return;

                    expression.GetterKey = selectedKey;
                    expression.Inputs = new List<ValueExpression>();
                    GUI.changed = true;
                    onChanged?.Invoke();
                });
            if (string.IsNullOrWhiteSpace(expression.GetterKey))
            {
                return;
            }

            int selectedIndex = entries.FindIndex(entry =>
                string.Equals(entry.Key, expression.GetterKey, StringComparison.Ordinal));
            if (selectedIndex < 0)
            {
                EditorGUILayout.HelpBox($"'{expression.GetterKey}' is not available on this unit.", MessageType.Warning);
                return;
            }

            UnitSourceGetSchemaEntry selected = entries[selectedIndex];
            expression.GetterKey = selected.Key;
            EnsureExpressionCount(ref expression.Inputs, selected.Parameters);
            for (int i = 0; i < selected.Parameters.Count; i++)
                DrawInput(expression.Inputs[i], selected.Parameters[i], depth + 1, sourceSchema, onChanged);
        }

        private static void DrawCompareInputs(
            ConditionConfig condition,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            List<string> compareKeys = s_expressionFactory.CompareTypeKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            if (compareKeys.Count == 0)
            {
                EditorGUILayout.HelpBox("No compare types are registered.", MessageType.Error);
                return;
            }

            int selectedIndex = Mathf.Max(0, compareKeys.IndexOf(condition.CompareType));
            selectedIndex = EditorGUILayout.Popup("Compare", selectedIndex, compareKeys.ToArray());
            condition.CompareType = compareKeys[selectedIndex];
            if (!s_expressionFactory.TryCreateCompareType(condition.CompareType, out ICompareType compareType))
            {
                EditorGUILayout.HelpBox($"Unknown compare type: {condition.CompareType}", MessageType.Error);
                return;
            }

            EnsureExpressionCount(ref condition.Inputs, compareType.Parameters);
            for (int i = 0; i < compareType.Parameters.Count; i++)
                DrawInput(condition.Inputs[i], compareType.Parameters[i], 0, sourceSchema, onChanged);
        }

        private static void DrawOperation(
            ValueExpression expression,
            UnitValueCategory expectedCategory,
            int depth,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            if (depth >= 6)
            {
                EditorGUILayout.HelpBox("Expression nesting is limited to 6 levels.", MessageType.Warning);
                return;
            }

            List<IValueOperation> operations = s_expressionFactory.ValueOperationKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => s_expressionFactory.TryCreateValueOperation(key, out IValueOperation operation) ? operation : null)
                .Where(operation => operation != null &&
                    (expectedCategory == UnitValueCategory.Any || operation.ResultCategory == expectedCategory))
                .ToList();
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox($"No operation returns {expectedCategory}.", MessageType.Warning);
                return;
            }

            int index = Mathf.Max(0, operations.FindIndex(operation =>
                string.Equals(GetOperationKey(operation), expression.OperationType, StringComparison.Ordinal)));
            index = EditorGUILayout.Popup("Operation", index, operations.Select(GetOperationKey).ToArray());
            IValueOperation selected = operations[index];
            expression.OperationType = GetOperationKey(selected);
            EnsureExpressionCount(ref expression.Inputs, selected.Parameters);
            for (int i = 0; i < selected.Parameters.Count; i++)
                DrawInput(expression.Inputs[i], selected.Parameters[i], depth + 1, sourceSchema, onChanged);
        }

        private static void DrawInput(
            ValueExpression expression,
            ComparatorParameterDefinition parameter,
            int depth,
            UnitSourceSchema sourceSchema,
            Action onChanged)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{parameter.Name} ({parameter.Category})", EditorStyles.miniBoldLabel);
            expression.Kind = (ValueExpressionKind)EditorGUILayout.EnumPopup("Value Kind", expression.Kind);
            if (expression.Kind == ValueExpressionKind.Literal)
                DrawLiteral(expression, parameter.Category);
            else if (expression.Kind == ValueExpressionKind.Getter)
                DrawGetter(expression, parameter.Category, depth, sourceSchema, onChanged);
            else if (expression.Kind == ValueExpressionKind.Operation)
                DrawOperation(expression, parameter.Category, depth, sourceSchema, onChanged);
            else
                expression.Kind = ValueExpressionKind.Literal;
            EditorGUILayout.EndVertical();
        }

        private static string GetOperationKey(IValueOperation operation)
        {
            return s_expressionFactory.ValueOperationKeys.FirstOrDefault(key =>
                s_expressionFactory.TryCreateValueOperation(key, out IValueOperation candidate) &&
                candidate.GetType() == operation.GetType()) ?? string.Empty;
        }

        private static UnitValueCategory GetConcreteCategory(UnitValueCategory category)
        {
            return category is UnitValueCategory.Bool or UnitValueCategory.Number or UnitValueCategory.Float2 or
                UnitValueCategory.Float3 or UnitValueCategory.Entity or UnitValueCategory.String
                ? category
                : UnitValueCategory.Number;
        }

        public static UnitValue CreateDefaultLiteral(UnitValueCategory category)
        {
            if (category == UnitValueCategory.Bool)
                return UnitValue.FromBool(false);
            if (category == UnitValueCategory.Float2)
                return UnitValue.FromFloat2(float2.zero);
            if (category == UnitValueCategory.Float3)
                return UnitValue.FromFloat3(float3.zero);
            if (category == UnitValueCategory.Entity)
                return UnitValue.FromEntity(Entity.Null);
            if (category == UnitValueCategory.String)
                return UnitValue.FromString(string.Empty);

            return UnitValue.FromFloat(0f);
        }

        private static void EnsureExpressionCount(
            ref List<ValueExpression> expressions,
            IReadOnlyList<ComparatorParameterDefinition> parameters)
        {
            expressions ??= new List<ValueExpression>();
            while (expressions.Count < parameters.Count)
            {
                expressions.Add(new ValueExpression
                {
                    Literal = CreateDefaultLiteral(parameters[expressions.Count].Category),
                });
            }

            if (expressions.Count > parameters.Count)
                expressions.RemoveRange(parameters.Count, expressions.Count - parameters.Count);

            for (int i = 0; i < expressions.Count; i++)
            {
                expressions[i] ??= new ValueExpression
                {
                    Literal = CreateDefaultLiteral(parameters[i].Category),
                };
            }
        }

        private static ComparatorFactory CreateExpressionFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }
    }
}
