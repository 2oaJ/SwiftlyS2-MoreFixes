namespace ZombiEden.CS2.SwiftlyS2.Fixes.Interface
{
    /// <summary>
    /// 重力触发修复服务
    /// </summary>
    public interface IGravityTouchFixService : IGameFixService
    {
        /// <summary>
        /// 设置实体的重力值
        /// </summary>
        void SetEntityGravity(uint entityId, float gravity);

        /// <summary>
        /// 移除实体的重力值
        /// </summary>
        void RemoveEntityGravity(uint entityId);

        /// <summary>
        /// 获取实体的重力值
        /// </summary>
        float? GetEntityGravity(uint entityId);

        /// <summary>
        /// 清除所有重力设置
        /// </summary>
        void ClearAllGravity();
    }
}