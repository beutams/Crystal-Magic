using System;
using System.Globalization;

/// <summary>Per-line progress, shared by the interaction runner and its presentation.</summary>
public sealed class DialoguePlayback
{
    private readonly int[] _visibleCharacterEnds;
    private double _elapsed;
    private double _waitElapsed;

    public DialoguePlayback(string text, float charactersPerSecond, float waitAfterTyping = 0f)
    {
        Text = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        CharactersPerSecond = float.IsNaN(charactersPerSecond) || float.IsInfinity(charactersPerSecond)
            ? 20f : Math.Max(1f, Math.Min(120f, charactersPerSecond));
        WaitAfterTyping = Text.Length == 0 ? 0f
            : float.IsNaN(waitAfterTyping) || float.IsInfinity(waitAfterTyping)
                ? 1.2f : Math.Max(0f, Math.Min(30f, waitAfterTyping));
        int[] elements = StringInfo.ParseCombiningCharacters(Text);
        _visibleCharacterEnds = new int[elements.Length];
        int characterCount = 0;
        for (int element = 0; element < elements.Length; element++)
        {
            int end = element + 1 < elements.Length ? elements[element + 1] : Text.Length;
            for (int i = elements[element]; i < end; i++)
            {
                if (char.IsHighSurrogate(Text[i]) && i + 1 < end && char.IsLowSurrogate(Text[i + 1]))
                    i++;
                characterCount++;
            }
            _visibleCharacterEnds[element] = characterCount;
        }
    }

    public string Text { get; }
    public float CharactersPerSecond { get; }
    public float WaitAfterTyping { get; }
    public int RevealedElements { get; private set; }
    public int VisibleCharacters => RevealedElements == 0 ? 0 : _visibleCharacterEnds[RevealedElements - 1];
    public bool IsCancelled { get; private set; }
    public bool IsTypingCompleted => !IsCancelled && RevealedElements == _visibleCharacterEnds.Length;
    public bool IsCompleted => IsTypingCompleted && _waitElapsed + 0.000001 >= WaitAfterTyping;

    public void Advance(float deltaTime)
    {
        if (IsCompleted || IsCancelled || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            return;
        if (!IsTypingCompleted)
        {
            _elapsed += deltaTime;
            RevealedElements = (int)Math.Min(_visibleCharacterEnds.Length, Math.Floor(_elapsed * CharactersPerSecond + 0.000001));
            // Start waiting on the next tick: even a long frame must display the full line
            // for the configured duration instead of consuming the wait before rendering it.
            return;
        }
        _waitElapsed += deltaTime;
    }

    public void Cancel() => IsCancelled = true;
}
