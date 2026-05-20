using SwiftlyS2.Shared;
using SwiftlyS2.Shared.SchemaDefinitions;
using System.Runtime.InteropServices;

namespace ZombiEden.CS2.SwiftlyS2.Fixes;

public static partial class GameFunctions
{
    [LibraryImport("swiftlys2", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial nint GetPureInterface(string iface_name);

    private const string GAMERESOURCESERVICESERVER_INTERFACE_VERSION = "GameResourceServiceServerV001";

    private delegate nint CGameEntitySystem__FindEntityByName_t(nint pEntitySystem, nint pStartEntity, nint pszName, nint pSearchingEntity, nint pActivator, nint pCaller, nint pFilter);

    private static ISwiftlyCore? _core;
    private static nint _pEntitySystem;
    private static CGameEntitySystem__FindEntityByName_t? _fnCGameEntitySystem__FindEntityByName;

    public static void Setup(ISwiftlyCore core)
    {
        _core = core;
        _fnCGameEntitySystem__FindEntityByName = Marshal.GetDelegateForFunctionPointer<CGameEntitySystem__FindEntityByName_t>(core.GameData.GetSignature("CGameEntitySystem_FindEntityByName"));

        unsafe
        {
            var offset_GameEntitySystem = core.GameData.GetOffset("GameEntitySystem");
            var pGameResourceServiceServer = GetPureInterface(GAMERESOURCESERVICESERVER_INTERFACE_VERSION);
            _pEntitySystem = *(nint*)(pGameResourceServiceServer + offset_GameEntitySystem);
        }
    }

    public static CBaseEntity? UTIL_FindEntityByName(CEntityInstance? startEntity, string szName, CEntityInstance? searchingEntity = null, CEntityInstance? activator = null, CEntityInstance? caller = null, nint pFilter = 0)
    {
        var pszName = Marshal.StringToHGlobalAnsi(szName);
        var res = _fnCGameEntitySystem__FindEntityByName!(_pEntitySystem, startEntity?.Address ?? 0, pszName, searchingEntity?.Address ?? 0, activator?.Address ?? 0, caller?.Address ?? 0, pFilter);
        if (res != 0)
        {
            return _core!.EntitySystem.GetEntityByAddress(res)!.As<CBaseEntity>();
        }

        return null;
    }
}
