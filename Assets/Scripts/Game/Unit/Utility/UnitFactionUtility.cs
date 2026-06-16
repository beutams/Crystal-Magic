public static class UnitFactionUtility
{
    public static bool IsEnemy(UnitFactionType self, UnitFactionType other)
    {
        if (self == UnitFactionType.Enemy)
            return other != UnitFactionType.Enemy;

        return other == UnitFactionType.Enemy;
    }
}
