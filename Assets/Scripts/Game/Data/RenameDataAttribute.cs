using System;

namespace CrystalMagic.Game.Data
{
    /// <summary>
    /// 标记该配置行类型仅由代码或其它流程维护，不在 Data Table Viewer 中列出或编辑。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class RenameDataAttribute : Attribute
    {
    }
}
