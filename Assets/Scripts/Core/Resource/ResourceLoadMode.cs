namespace CrystalMagic.Core {
    /// <summary>
    /// 资源加载模式
    /// </summary>
    public enum ResourceLoadMode
    {
        /// <summary>
        /// 编辑器模式：直接从 Assets 加载
        /// </summary>
        Editor,

        /// <summary>
        /// AB 包模式：从 AB 包加载（打包时）
        /// </summary>
        AssetBundle
    }
}
