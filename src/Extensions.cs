using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Diagnostics.CodeAnalysis;

namespace ZombiEden.CS2.SwiftlyS2.Fixes
{
    public static class Extensions
    {
        public static bool IsPlayerAlive(this CCSPlayerPawn? pawn)
        {
            if (!pawn.Valid())
            {
                return false;
            }

            return (LifeState_t)pawn.LifeState == LifeState_t.LIFE_ALIVE;
        }

        public static bool Valid([NotNullWhen(true)] this CBasePlayerPawn? pawn)
        {
            try
            {
                if (pawn is null)
                {
                    return false;
                }
                if (!pawn.IsValid)
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static matrix3x4_t EntityToWorldTransform(this CGameSceneNode node)
        {
            matrix3x4_t mat = new();

            // 获取绝对旋转和位置
            QAngle angles = node.AbsRotation;
            Vector pos = node.AbsOrigin;

            // 使用QAngle的属性直接访问
            float yawRad = angles.Yaw * MathF.PI / 180f;   // 使用MathF.PI / 180f 作为DEG2RAD
            float pitchRad = angles.Pitch * MathF.PI / 180f;
            float rollRad = angles.Roll * MathF.PI / 180f;

            float sy = MathF.Sin(yawRad);
            float cy = MathF.Cos(yawRad);
            float sp = MathF.Sin(pitchRad);
            float cp = MathF.Cos(pitchRad);
            float sr = MathF.Sin(rollRad);
            float cr = MathF.Cos(rollRad);

            // 预计算
            float crcy = cr * cy;
            float crsy = cr * sy;
            float srcy = sr * cy;
            float srsy = sr * sy;

            // 填充矩阵（第0行）
            mat[0, 0] = cp * cy;
            mat[0, 1] = sp * srcy - crsy;
            mat[0, 2] = sp * crcy + srsy;
            mat[0, 3] = pos.X;

            // 填充矩阵（第1行）
            mat[1, 0] = cp * sy;
            mat[1, 1] = sp * srsy + crcy;
            mat[1, 2] = sp * crsy - srcy;
            mat[1, 3] = pos.Y;

            // 填充矩阵（第2行）
            mat[2, 0] = -sp;
            mat[2, 1] = sr * cp;
            mat[2, 2] = cr * cp;
            mat[2, 3] = pos.Z;

            return mat;
        }
    }
}
