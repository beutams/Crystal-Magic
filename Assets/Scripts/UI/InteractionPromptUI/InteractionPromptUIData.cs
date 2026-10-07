// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class InteractionPromptUIData : UIData
{
    public UINode Prompt;
    public UINode Prompt_Label;

    public override void Bind(Transform root)
    {
        Prompt = UINode.From(Find(root, "Prompt"));
        Prompt_Label = UINode.From(Find(root, "Prompt/Label"));
    }
}
