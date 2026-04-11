namespace CrystalMagic.Core {
    /// <summary>
    /// 转场 UI 接口
    /// </summary>
    public interface ITransitionUI
    {
        /// <summary>
        /// 显示转场界面
        /// </summary>
        System.Collections.IEnumerator Show();

        /// <summary>
        /// 隐藏转场界面
        /// </summary>
        System.Collections.IEnumerator Hide();
    }
}
