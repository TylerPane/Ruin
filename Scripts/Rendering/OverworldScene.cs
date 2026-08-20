using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuinGamePDT.Party;
using RuinGamePDT.Resources;
using RuinGamePDT.World;

namespace RuinGamePDT.Rendering;

public class OverworldScene(WorldData world, Banner banner, Texture2D pixel)
{
    private const int TileSize = 16;

    private KeyboardState _prevKeyboard;
    private MouseState _prevMouse;
    private int _viewportWidth;
    private int _viewportHeight;

    // Screen-space top-left of the tile the banner stands on; banner is always centered on screen.
    private int CameraOffsetX => _viewportWidth / 2 - banner.X * TileSize - TileSize / 2;
    private int CameraOffsetY => _viewportHeight / 2 - banner.Y * TileSize - TileSize / 2;

    public void Update(MouseState mouse, KeyboardState keyboard, int viewportWidth, int viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;

        HandleKeyboard(keyboard);
        HandleMouseClick(mouse);

        _prevKeyboard = keyboard;
        _prevMouse = mouse;
    }

    private void HandleKeyboard(KeyboardState kb)
    {
        int dx = 0, dy = 0;
        if (JustPressed(kb, Keys.Up) || JustPressed(kb, Keys.W)) dy -= 1;
        if (JustPressed(kb, Keys.Down) || JustPressed(kb, Keys.S)) dy += 1;
        if (JustPressed(kb, Keys.Left) || JustPressed(kb, Keys.A)) dx -= 1;
        if (JustPressed(kb, Keys.Right) || JustPressed(kb, Keys.D)) dx += 1;

        if (dx != 0 || dy != 0)
            OverworldMovement.Step(banner, world, dx, dy);
    }

    private void HandleMouseClick(MouseState mouse)
    {
        bool justClicked = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (!justClicked) return;

        int tx = (int)MathF.Floor((mouse.X - CameraOffsetX) / (float)TileSize);
        int ty = (int)MathF.Floor((mouse.Y - CameraOffsetY) / (float)TileSize);

        OverworldMovement.MoveToAdjacent(banner, world, tx, ty);
    }

    private bool JustPressed(KeyboardState kb, Keys k) => kb.IsKeyDown(k) && !_prevKeyboard.IsKeyDown(k);

    public void Draw(SpriteBatch sb)
    {
        DrawTerrain(sb);
        DrawBanner(sb);
    }

    private void DrawTerrain(SpriteBatch sb)
    {
        int tilesX = _viewportWidth / TileSize / 2 + 2;
        int tilesY = _viewportHeight / TileSize / 2 + 2;

        for (int x = banner.X - tilesX; x <= banner.X + tilesX; x++)
        for (int y = banner.Y - tilesY; y <= banner.Y + tilesY; y++)
        {
            var tile = world.GetTile(x, y);
            var color = tile is { IsScouted: true } ? BiomeColor(tile.Biome) : new Color(40, 40, 40);
            sb.Draw(pixel, new Rectangle(CameraOffsetX + x * TileSize, CameraOffsetY + y * TileSize, TileSize, TileSize), color);
        }
    }

    private static Color BiomeColor(BiomeType biome) => biome switch
    {
        BiomeType.Forest    => new Color(34, 102, 51),
        BiomeType.Plains    => new Color(150, 180, 90),
        BiomeType.Desert    => new Color(210, 190, 110),
        BiomeType.Swamp     => new Color(80, 100, 70),
        BiomeType.Mountains => new Color(120, 120, 130),
        _                   => Color.Gray
    };

    private void DrawBanner(SpriteBatch sb)
    {
        var rect = new Rectangle(CameraOffsetX + banner.X * TileSize, CameraOffsetY + banner.Y * TileSize, TileSize, TileSize);
        sb.Draw(pixel, rect, Color.Gold);
    }
}
