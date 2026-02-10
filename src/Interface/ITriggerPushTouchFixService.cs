using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Interface
{
    /// <summary>
    /// TriggerPush触摸修复服务
    /// </summary>
    public interface ITriggerPushTouchFixService : IGameFixService
    {
        /// <summary>
        /// 检查是否通过触发器过滤器
        /// </summary>
        bool PassesTriggerFilters(CBaseTrigger trigger, CBaseEntity entity);
    }
}