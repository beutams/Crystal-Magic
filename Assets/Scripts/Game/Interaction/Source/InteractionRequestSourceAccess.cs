using System;

public delegate bool InteractionRequestGetHandler(out InteractionRequestSnapshot request);

public sealed class InteractionRequestSourceGet
{
    public InteractionRequestSourceGet(string key, InteractionRequestGetHandler invoke)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Interaction request source key cannot be empty.", nameof(key));

        Key = key;
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public string Key { get; }
    public InteractionRequestGetHandler Invoke { get; }

    public bool TryGet(out InteractionRequestSnapshot request)
    {
        return Invoke(out request) && request.IsValid;
    }
}

public readonly struct InteractionRequestGetSchemaEntry
{
    public InteractionRequestGetSchemaEntry(string key, Type componentType)
    {
        Key = key;
        ComponentType = componentType;
    }

    public string Key { get; }
    public Type ComponentType { get; }
}
