using System;

namespace CrystalMagic.Core {
    /// <summary>
    /// 标记一个类为游戏配置，ConfigEditorWindow 会自动扫描并展示
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GameConfigAttribute : Attribute { }
}
