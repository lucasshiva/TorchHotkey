using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using TorchHotkey.Common.Configs;

namespace TorchHotkey;

public class TorchHotkeyPlayer : ModPlayer
{
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (TorchHotkey.TorchKeybiding.JustPressed)
        {
            PlaceTorch();
        }
    }

    public void PlaceTorch()
    {
        Item torch = FindTorch();
        if (torch == null)
            return;

        SmartPlaceTorch(torch);
    }

    private void SmartPlaceTorch(Item torch)
    {
        if (Main.SmartCursorIsUsed)
        {
            // If this fails, we move on to check for additional tiles.
            // This way, we can place torches while holding a tool with smart cursor enabled.
            if (TryPlaceTorchAt(torch, Main.SmartCursorX, Main.SmartCursorY))
                return;
        }

        (int mouseI, int mouseJ) = Main.MouseWorld.ToTileCoordinates();

        if (TryPlaceTorchAt(torch, mouseI, mouseJ))
            return;

        var config = ModContent.GetInstance<TorchHotkeyConfig>();
        if (!config.EnableSmartPlacementToggle)
            return;

        int radius = config.SmartPlacementRadius;

        List<(float dist, Point p)> candidates = [];
        for (int i = mouseI - radius; i <= mouseI + radius; i++)
        {
            for (int j = mouseJ - radius; j <= mouseJ + radius; j++)
            {
                float dist = Vector2.Distance(Main.MouseWorld, new Vector2(i * 16, j * 16));
                if (dist <= radius * 16)
                    candidates.Add((dist, new Point(i, j)));
            }
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        foreach (var (_, p) in candidates)
        {
            if (TryPlaceTorchAt(torch, p.X, p.Y))
                return;
        }
    }

    private bool TryPlaceTorchAt(Item torch, int i, int j)
    {
        if (!WorldGen.InWorld(i, j))
            return false;

        Tile tile = Main.tile[i, j];

        // Only attempt placement on empty tiles
        if (tile.HasTile)
        {
            // Allow cuttable tiles (grass, vines, etc.)
            if (!Main.tileCut[tile.TileType])
                return false;

            WorldGen.KillTile(i, j, noItem: true);
        }

        var before = (tile.TileType, tile.WallType, tile.HasTile);

        if (!WorldGen.PlaceTile(i, j, torch.createTile, style: torch.placeStyle))
            return false;

        // NOTE: I think this only applies to the player's inventory, so it won't work for modded bags.
        // We might not even need this.
        Player.ItemCheck();

        // I don't remember why we do this check, but it's also done in `HelpfulHotkeys` mod.
        // If I'm not mistaken, it doesn't show an animation if no torches have been placed.
        var after = (tile.TileType, tile.WallType, tile.HasTile);
        if (before == after)
            return false;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendTileSquare(-1, i, j, 1);

        torch.stack--;
        if (torch.stack <= 0)
            torch.TurnToAir();

        return true;
    }

    [CanBeNull]
    private Item FindTorch()
    {
        Item sackTorch = TryFindTorchInSlayersSack();
        if (sackTorch != null)
            return sackTorch;

        foreach (Item item in Player.inventory)
        {
            if (IsTorch(item))
                return item;
        }

        return null;
    }

    [CanBeNull]
    private Item TryFindTorchInSlayersSack()
    {
        if (!ModLoader.TryGetMod("VacuumBags", out Mod vacuumBags))
            return null;

        Type sackType = vacuumBags.Code.GetType("VacuumBags.Items.SlayersSack");
        if (sackType == null)
            return null;

        MethodInfo chooseTorchMethod = sackType.GetMethod(
            "ChooseTorchFromSack",
            BindingFlags.Static | BindingFlags.Public
        );

        if (chooseTorchMethod == null)
            return null;

        Func<Item, bool> torchCondition = IsTorch;
        object result = chooseTorchMethod.Invoke(null, [Player, torchCondition]);

        if (result is not Item item)
            return null;

        return item;
    }

    private static bool IsTorch(Item item)
    {
        return item.stack > 0
            && item.createTile >= TileID.Torches
            && TileID.Sets.Torch[item.createTile];
    }
}
