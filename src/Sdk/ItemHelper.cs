using SwiftlyS2.Shared.SchemaDefinitions;

namespace ZombiEden.CS2.SwiftlyS2.Fixes.Sdk;

public static class ItemHelper
{
    public readonly static Dictionary<string, gear_slot_t> WeaponGearSlotDict = new()
    {
        // ========== 手枪 (GEAR_SLOT_PISTOL) ==========
        ["weapon_deagle"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_elite"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_fiveseven"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_glock"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_tec9"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_hkp2000"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_p250"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_usp_silencer"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_cz75a"] = gear_slot_t.GEAR_SLOT_PISTOL,
        ["weapon_revolver"] = gear_slot_t.GEAR_SLOT_PISTOL,

        // ========== 步枪 (GEAR_SLOT_RIFLE) ==========
        ["weapon_ak47"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_aug"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_awp"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_famas"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_g3sg1"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_galilar"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_m249"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_m4a1"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_mac10"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_p90"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_mp5sd"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_ump45"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_xm1014"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_bizon"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_mag7"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_negev"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_sawedoff"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_mp7"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_mp9"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_nova"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_scar20"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_sg556"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_ssg08"] = gear_slot_t.GEAR_SLOT_RIFLE,
        ["weapon_m4a1_silencer"] = gear_slot_t.GEAR_SLOT_RIFLE,

        // ========== 手雷 (GEAR_SLOT_GRENADES) ==========
        ["weapon_flashbang"] = gear_slot_t.GEAR_SLOT_GRENADES,
        ["weapon_hegrenade"] = gear_slot_t.GEAR_SLOT_GRENADES,
        ["weapon_smokegrenade"] = gear_slot_t.GEAR_SLOT_GRENADES,
        ["weapon_molotov"] = gear_slot_t.GEAR_SLOT_GRENADES,
        ["weapon_decoy"] = gear_slot_t.GEAR_SLOT_GRENADES,
        ["weapon_incgrenade"] = gear_slot_t.GEAR_SLOT_GRENADES,

        // ========== 刀 (GEAR_SLOT_KNIFE) ==========
        ["weapon_taser"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knifegg"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_t"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_bayonet"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_css"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_flip"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_gut"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_karambit"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_m9_bayonet"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_tactical"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_falchion"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_survival_bowie"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_butterfly"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_push"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_cord"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_canis"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_ursus"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_gypsy_jackknife"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_outdoor"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_stiletto"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_widowmaker"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_skeleton"] = gear_slot_t.GEAR_SLOT_KNIFE,
        ["weapon_knife_kukri"] = gear_slot_t.GEAR_SLOT_KNIFE,

        // ========== C4 (GEAR_SLOT_C4) ==========
        ["weapon_c4"] = gear_slot_t.GEAR_SLOT_C4,

        // ========== 装备/实用工具 (GEAR_SLOT_UTILITY) ==========
        ["item_kevlar"] = gear_slot_t.GEAR_SLOT_UTILITY,
        ["item_assaultsuit"] = gear_slot_t.GEAR_SLOT_UTILITY,
        ["item_heavyassaultsuit"] = gear_slot_t.GEAR_SLOT_UTILITY,
        ["item_defuser"] = gear_slot_t.GEAR_SLOT_UTILITY,
        ["ammo_50ae"] = gear_slot_t.GEAR_SLOT_UTILITY
    };
}
