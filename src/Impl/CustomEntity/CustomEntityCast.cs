using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Impl.CustomEntity;

public static class CustomEntityCast
{
    public static CPointViewControl? AsPointViewControl(this CEntityInstance? entityInstance)
    {
        if (entityInstance is null || !entityInstance.IsValid)
        {
            return null;
        }

        var designerName = entityInstance.DesignerName;
        if (!string.Equals(designerName, "logic_relay", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var privateVScripts = entityInstance.PrivateVScripts;
        if (!string.IsNullOrWhiteSpace(privateVScripts) && privateVScripts.Contains("point_viewcontrol", StringComparison.OrdinalIgnoreCase))
        {
            return new CPointViewControl(entityInstance.As<CLogicRelay>());
        }

        return null;
    }
}
