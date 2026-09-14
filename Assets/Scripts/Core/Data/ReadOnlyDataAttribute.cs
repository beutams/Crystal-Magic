using System;

namespace CrystalMagic.Core {
    /// <summary>
    /// 标记该配置行类型仅由代码或其它流程维护，不在数据编辑器中列出或编辑。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ReadOnlyDataAttribute : Attribute
    {
    }
}
