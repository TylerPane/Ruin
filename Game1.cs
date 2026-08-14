using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.Generation;
using RuinGamePDT.Party;
using RuinGamePDT.Rendering;
using RuinGamePDT.Resources;
using RuinGamePDT.Weapons;
using RuinGamePDT.World;

namespace RuinGamePDT;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private EncounterState _encounterState = null!;
    private TurnManager _turnManager = null!;
    private EncounterScene _scene = null!;
    private EnemyAI _ai = null!;
    private Dictionary<string, Texture2D> _skillIcons = null!;
    private SpriteFont _logFont = null!;
    private EncounterResult _encounterResult = EncounterResult.Ongoing;

    private WorldData _world = null!;
    private Banner _banner = null!;
    private OverworldScene _overworldScene = null!;
    private PartyWindow _partyWindow = null!;
    private StartScreen _startScreen = null!;
    private KeyboardState _prevKeyboard;

    private enum SceneMode { StartScreen, Overworld, Encounter }
    private SceneMode _sceneMode = SceneMode.StartScreen;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1920,
            PreferredBackBufferHeight = 900
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _logFont = Content.Load<SpriteFont>("Fonts/CombatLogFont");

        _skillIcons = new Dictionary<string, Texture2D>
        {
            { "Punch", LoadTexture("Content/Icons/Punch.png") },
            { "Throw Stone", LoadTexture("Content/Icons/StoneThrow.png") },
            { "Shout", LoadTexture("Content/Icons/Shout.png") },
            { "Rush", LoadTexture("Content/Icons/Rush.png") },
            { "Defensive Stance", LoadTexture("Content/Icons/DStance.png") },
            { "First Aid", LoadTexture("Content/Icons/FirstAid.png") }
        };

        var map = new EncounterMapGenerator().Generate(BiomeType.Plains, seed: 42);
        _encounterState = new EncounterState(map);

        var merc1 = Mercenary.CreateRandom();
        var merc2 = Mercenary.CreateRandom();
        var enemy = new PricklebackGoblin();

        _encounterState.Mercenaries.Add(merc1);
        _encounterState.Mercenaries.Add(merc2);
        _encounterState.Enemies.Add(enemy);

        _encounterState.PlaceCreature(merc1, 0, 0);
        _encounterState.PlaceCreature(merc2, 1, 0);

        // Try to place enemy; find a free tile if (49,49) is blocked
        bool placed = _encounterState.PlaceCreature(enemy, 49, 49);
        if (!placed)
        {
            for (int x = 49; x >= 0 && !placed; x--)
                for (int y = 49; y >= 0 && !placed; y--)
                    placed = _encounterState.PlaceCreature(enemy, x, y);
        }

        _turnManager = new TurnManager(_encounterState);
        _turnManager.StartEncounter();

        var resolver = new CombatResolver(Random.Shared.Next);
        _scene = new EncounterScene(_encounterState, _turnManager, _pixel, resolver, _skillIcons, _logFont);
        _ai = new EnemyAI(resolver);

        _partyWindow = new PartyWindow();
        _startScreen = new StartScreen();
    }

    private void StartOverworld(WorldSize size)
    {
        _world = new WorldGenerator().GenerateWorld((int)size, (int)size, seed: 42);
        _banner = new Banner();
        _banner.AddMercenary(Mercenary.CreateRandom());
        _banner.AddMercenary(Mercenary.CreateRandom());
        _banner.Inventory.Add(new Sword());
        _banner.Inventory.Add(new Bow());
        OverworldMovement.ScoutAround(_banner, _world);
        _overworldScene = new OverworldScene(_world, _banner, _pixel);
        _sceneMode = SceneMode.Overworld;
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();

        if (_sceneMode == SceneMode.StartScreen)
        {
            _startScreen.Update(Mouse.GetState(), kb, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            if (_startScreen.Confirmed)
                StartOverworld(_startScreen.Selected);
            _prevKeyboard = kb;
            base.Update(gameTime);
            return;
        }

        if (JustPressed(kb, Keys.P))
            _partyWindow.Toggle();
        if (_partyWindow.IsOpen)
        {
            if (JustPressed(kb, Keys.Escape))
                _partyWindow.Close();
            _prevKeyboard = kb;
            base.Update(gameTime);
            return;
        }

        if (_sceneMode == SceneMode.Overworld)
        {
            _overworldScene.Update(Mouse.GetState(), kb, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _prevKeyboard = kb;
            base.Update(gameTime);
            return;
        }

        if (_encounterResult != EncounterResult.Ongoing)
        {
            _prevKeyboard = kb;
            base.Update(gameTime);
            return;
        }

        _scene.Update(Mouse.GetState(), GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        if (_turnManager.CurrentCreature is not Mercenary && _turnManager.CanMove(_turnManager.CurrentCreature!))
        {
            var enemy = _turnManager.CurrentCreature!;
            var entries = _ai.TakeTurn(enemy, _encounterState);
            _scene.AddCombatLogEntries(entries);
            _turnManager.EndCreatureTurn(enemy);
        }

        _encounterResult = _turnManager.CheckEndCondition();

        _prevKeyboard = kb;
        base.Update(gameTime);
    }

    private bool JustPressed(KeyboardState kb, Keys k) => kb.IsKeyDown(k) && !_prevKeyboard.IsKeyDown(k);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin();

        if (_sceneMode == SceneMode.StartScreen)
        {
            _startScreen.Draw(_spriteBatch, _pixel, _logFont, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else if (_sceneMode == SceneMode.Overworld)
        {
            _overworldScene.Draw(_spriteBatch);
            _partyWindow.Draw(_spriteBatch, _pixel, _logFont, _banner, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }
        else
        {
            _scene.Draw(_spriteBatch);
            if (_encounterResult != EncounterResult.Ongoing)
                DrawEndMessage(_spriteBatch);
            _partyWindow.Draw(_spriteBatch, _pixel, _logFont, _banner, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawEndMessage(SpriteBatch sb)
    {
        string message = _encounterResult switch
        {
            EncounterResult.Victory => "VICTORY",
            EncounterResult.Defeat => "DEFEAT",
            _ => _encounterResult.ToString().ToUpperInvariant()
        };

        var textSize = _logFont.MeasureString(message);
        var viewport = GraphicsDevice.Viewport;
        var pos = new Vector2((viewport.Width - textSize.X) / 2f, (viewport.Height - textSize.Y) / 2f);

        sb.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.5f);
        sb.DrawString(_logFont, message, pos, Color.White);
    }

    private Texture2D LoadTexture(string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            return Texture2D.FromStream(GraphicsDevice, stream);
        }
    }
}
