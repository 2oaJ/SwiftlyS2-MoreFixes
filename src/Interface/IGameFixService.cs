namespace ZombiEden.CS2.SwiftlyS2.Fixes.Interface
{
    /// <summary>
    /// 游戏修复服务基接口
    /// </summary>
    public interface IGameFixService
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// 安装修复
        /// </summary>
        void Install();

        /// <summary>
        /// 卸载修复
        /// </summary>
        void Uninstall();
    }
}