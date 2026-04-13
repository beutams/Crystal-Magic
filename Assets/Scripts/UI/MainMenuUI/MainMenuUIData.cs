// AUTO-GENERATED — DO NOT EDIT MANUALLY
// 输出目录：Assets/Scripts/UI/{Prefab名}/ — 右键 Prefab → Assets/Tools/Generate UIData 重新生成

using UnityEngine;
using CrystalMagic.Core;

public class MainMenuUIData : UIData
{
    public UINode Start;
    public UINode Start_TextTMP;
    public UINode Load;
    public UINode Load_TextTMP;
    public UINode Config;
    public UINode Config_TextTMP;
    public UINode Exit;
    public UINode Exit_TextTMP;

    public override void Bind(Transform root)
    {
        Start = UINode.From(Find(root, "Start"));
        Start_TextTMP = UINode.From(Find(root, "Start/Text (TMP)"));
        Load = UINode.From(Find(root, "Load"));
        Load_TextTMP = UINode.From(Find(root, "Load/Text (TMP)"));
        Config = UINode.From(Find(root, "Config"));
        Config_TextTMP = UINode.From(Find(root, "Config/Text (TMP)"));
        Exit = UINode.From(Find(root, "Exit"));
        Exit_TextTMP = UINode.From(Find(root, "Exit/Text (TMP)"));
    }
}
