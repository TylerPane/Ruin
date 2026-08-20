using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuinGamePDT.World;

namespace RuinGamePDT.Rendering;

public class StartScreen
{
    private static readonly WorldSize[] Options =
    [
        WorldSize.Small,
        WorldSize.Medium,
        WorldSize.Large,
        WorldSize.Huge
    ];

    private const int OptionHeight = 32;
    private const int OptionGap = 8;

    private KeyboardState _prevKeyboard;
    private MouseState _prevMouse;

    public int SelectedIndex { get; private set; }
    public WorldSize Selected => Options[SelectedIndex];
    public bool Confirmed { get; private set; }

    public void Update(MouseState mouse, KeyboardState keyboard, int viewportWidth, int viewportHeight)
    {
        if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
            SelectedIndex = (SelectedIndex - 1 + Options.Length) % Options.Length;
        else if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
            SelectedIndex = (SelectedIndex + 1) % Options.Length;

        for (int i = 0; i < Options.Length; i++)
        {
            if (JustPressed(keyboard, Keys.D1 + i))
                SelectedIndex = i;
        }

        if (JustPressed(keyboard, Keys.Enter) || JustPressed(keyboard, Keys.Space))
            Confirmed = true;

        bool justClicked = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        if (justClicked)
        {
            int hovered = OptionIndexAt(mouse.X, mouse.Y, viewportWidth, viewportHeight);
            if (hovered >= 0)
            {
                SelectedIndex = hovered;
                Confirmed = true;
            }
        }

        _prevKeyboard = keyboard;
        _prevMouse = mouse;
    }

    private int OptionIndexAt(int x, int y, int viewportWidth, int viewportHeight)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            var bounds = GetOptionBounds(i, viewportWidth, viewportHeight);
            if (bounds.Contains(x, y)) return i;
        }
        return -1;
    }

    public static Rectangle GetOptionBounds(int index, int viewportWidth, int viewportHeight)
    {
        int totalHeight = Options.Length * OptionHeight + (Options.Length - 1) * OptionGap;
        int startY = (viewportHeight - totalHeight) / 2;
        int width = 240;
        int x = (viewportWidth - width) / 2;
        int y = startY + index * (OptionHeight + OptionGap);
        return new Rectangle(x, y, width, OptionHeight);
    }

    private bool JustPressed(KeyboardState kb, Keys k) => kb.IsKeyDown(k) && !_prevKeyboard.IsKeyDown(k);

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font, int viewportWidth, int viewportHeight)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            var bounds = GetOptionBounds(i, viewportWidth, viewportHeight);
            var color = i == SelectedIndex ? Color.Gold : Color.DarkSlateGray;
            sb.Draw(pixel, bounds, color);

            string label = $"{Options[i]} ({(int)Options[i]}x{(int)Options[i]})";
            var textSize = font.MeasureString(label);
            var textPos = new Vector2(
                bounds.X + (bounds.Width - textSize.X) / 2f,
                bounds.Y + (bounds.Height - textSize.Y) / 2f);
            sb.DrawString(font, label, textPos, Color.White);
        }
    }
}
