using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Interface
{
    /// <summary>
    /// 武器剥离修复服务
    /// </summary>
    public interface IStripFixService : IGameFixService
    {
        /// <summary>
        /// 剥离玩家所有武器
        /// </summary>
        bool StripPlayerWeapons(CCSPlayerPawn pawn);

        /// <summary>
        /// 剥离玩家特定槽位武器
        /// </summary>
        bool StripPlayerWeapons(CCSPlayerPawn pawn, HashSet<uint> stripSet);
    }
}