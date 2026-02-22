using Terraria.ModLoader;

namespace TorchHotkey
{
    // Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class TorchHotkey : Mod
    {
        internal static ModKeybind TorchKeybiding;

        public override void Load()
        {
            TorchKeybiding = KeybindLoader.RegisterKeybind(this, "PlaceTorch", "X");
        }

        public override void Unload()
        {
            TorchKeybiding = null;
        }
    }
}
