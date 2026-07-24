using SwiftlyS2.Shared.Natives;
using System.Runtime.InteropServices;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Sdk;

public static class GameTypes
{
    [StructLayout(LayoutKind.Sequential)]
    public struct InputData_t
    {
        public nint pActivator;
        public nint pCaller;
        public CVariant<CVariantDefaultAllocator> value;
        public int nOutputID;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CEntityPrecacheContext
    {
        public nint m_pKeyValues;
        public nint m_pConfig;
        public nint m_pManifest;
    };

    public const int SF_PLAYEREQUIP_STRIPFIRST = 0x0002;
    public const int SF_PLAYEREQUIP_ONLYSTRIPSAME = 0x0004;
}
