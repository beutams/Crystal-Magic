namespace CrystalMagic.Core {
    /// <summary>
    /// 配置表行基类
    /// 所有配置数据继承此类，Id 为主键
    /// </summary>
    [System.Serializable]
    public abstract class DataRow
    {
        public int Id;
    }
}
