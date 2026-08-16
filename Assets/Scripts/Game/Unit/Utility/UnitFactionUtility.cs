public static class UnitFactionUtility
{
    public static bool IsPlayer(UnitFactionType faction)
    {
        return faction == UnitFactionType.Player;
    }

    public static bool IsNpc(UnitFactionType faction)
    {
        return faction == UnitFactionType.Npc;
    }

    public static bool IsHostile(UnitFactionType faction)
    {
        return faction == UnitFactionType.Enemy || faction == UnitFactionType.Boss;
    }

    public static bool IsEnemy(UnitFactionType self, UnitFactionType other)
    {
        return IsCombatFaction(self) &&
               IsCombatFaction(other) &&
               IsHostile(self) != IsHostile(other);
    }

    private static bool IsCombatFaction(UnitFactionType faction)
    {
        return faction == UnitFactionType.Player ||
               faction == UnitFactionType.Friend ||
               IsHostile(faction);
    }
}
