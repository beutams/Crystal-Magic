using System;

public sealed class Comparator
{
    // Kept as a read-only data outlet until state-machine callers are migrated.
    public readonly Condition[] conditions;

    public Comparator(Condition[] conditions, bool isValid = true)
    {
        this.conditions = conditions ?? Array.Empty<Condition>();
        IsValid = isValid;
    }

    public bool IsValid { get; }

    public bool GetResult()
    {
        if (!IsValid)
            return false;

        for (int i = 0; i < conditions.Length; i++)
        {
            Condition condition = conditions[i];
            if (condition == null)
                return false;

            bool matches = condition.Compare();
            if (condition.Type == ConditionType.Necessary && !matches)
                return false;

            if (condition.Type == ConditionType.Unallowed && matches)
                return false;
        }

        return true;
    }
}
