// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Unit Component Sources

using System;
using System.Collections.Generic;

public static class UnitComponentSourceRegistry
{
    private static readonly UnitComponentSource[] s_sources =
    {
        new UnitAttackSource(),
        new UnitBuffSource(),
        new UnitControlSource(),
        new UnitDeathSource(),
        new UnitElementSource(),
        new UnitFacingSource(),
        new UnitFactionSource(),
        new UnitManaSource(),
        new UnitMoveSource(),
        new UnitPerceptionSource(),
        new UnitTransformSource(),
        new UnitVariableSource(),
        new UnitVitalitySource(),
    };

    public static IReadOnlyList<UnitComponentSource> Sources => s_sources;

    public static void BindAll(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        for (int i = 0; i < s_sources.Length; i++)
            s_sources[i].Bind(context, table);
    }

    public static UnitSourceSchema CreateSchema()
    {
        UnitSourceSchemaBuilder builder = new();
        for (int i = 0; i < s_sources.Length; i++)
            s_sources[i].Describe(builder);

        return builder.Build();
    }
}
