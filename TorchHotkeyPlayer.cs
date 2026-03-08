using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Terraria;
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
        var config = ModContent.GetInstance<TorchHotkeyConfig>();

        if (Main.SmartCursorShowing)
        {
            // If this fails, we move on to check for additional tiles.
            // This way, we can place torches while holding a tool with smart cursor enabled.
            //
            // We're assuming this is within the player's interaction range.
            if (TryPlaceTorchAt(torch, Main.SmartCursorX, Main.SmartCursorY))
                return;
        }

        (int mouseI, int mouseJ) = Main.MouseWorld.ToTileCoordinates();

        // Too far to place a torch at, don't even try.
        if (!IsTileInRange(mouseI, mouseJ))
            return;

        if (TryPlaceTorchAt(torch, mouseI, mouseJ))
            return;

        // Since we couldn't place a torch under cursor, we try adjacent tiles if enabled.
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
            // Enforce that we don't extend beyond the player's reach, even for adjacent tiles.
            if (!IsTileInRange(p.X, p.Y))
                continue;

            if (TryPlaceTorchAt(torch, p.X, p.Y))
                return;
        }
    }

    private static bool TryPlaceTorchAt(Item torch, int i, int j)
    {
        if (!WorldGen.InWorld(i, j))
            return false;

        Tile targetTile = Main.tile[i, j];

        if (targetTile.HasTile)
        {
            // Allow cuttable tiles (grass, vines, etc.)
            if (!Main.tileCut[targetTile.TileType])
                return false;

            WorldGen.KillTile(i, j, noItem: true);
        }

        // Saw this on Terraria's decompiled source code, might be useful.
        if (!TileLoader.CanPlace(i, j, TileID.Torches))
            return false;

        // `PlaceTile` doesn't respect player's reach, so we need additional checks elsewhere.
        WorldGen.PlaceTile(i, j, torch.createTile, style: torch.placeStyle);
        if (Main.tile[i, j].TileType != TileID.Torches)
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
        return sackTorch ?? Player.inventory.FirstOrDefault(IsTorch);
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

    private bool IsTileInRange(int i, int j)
    {
        var px = (int)(Player.Center.X / 16f);
        var py = (int)(Player.Center.Y / 16f);

        int dx = Math.Abs(i - px);
        int dy = Math.Abs(j - py);

        return dx <= Player.tileRangeX && dy <= Player.tileRangeY;
    }
}
