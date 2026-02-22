using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TorchHotkey.Common.Configs;

public class TorchHotkeyConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("SmartPlacement")]
    [DefaultValue(true)]
    public bool EnableSmartPlacementToggle;

    [DefaultValue(3)]
    [Range(2, 10)]
    public int SmartPlacementRadius;
}
