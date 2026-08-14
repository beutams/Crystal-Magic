using System;
using System.Collections.Generic;

[Serializable]
public class ValueExpression
{
    public ValueExpressionKind Kind = ValueExpressionKind.Literal;
    public UnitValue Literal = UnitValue.FromFloat(0f);
    public string GetterKey = string.Empty;
    public string OperationType = string.Empty;
    public List<ValueExpression> Inputs = new();
}

public enum ValueExpressionKind
{
    Literal,
    Getter,
    Operation,
}

// Serialized condition tree. Each input may be a literal, a bound getter, or another operation.
[Serializable]
public class ConditionConfig
{
    public ConditionType ConditionType = ConditionType.Necessary;
    public string CompareType = string.Empty;
    public List<ValueExpression> Inputs = new();

    // Temporary serialized fields for callers that have not migrated to Inputs yet.
    public string SourceType = string.Empty;
    public int SourceParam = -1;
    public float CompareValue;
}
