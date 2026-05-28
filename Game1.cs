using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.Generation;
using RuinGamePDT.Rendering;
using RuinGamePDT.Resources;

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

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
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

        _skillIcons = new Dictionary<string, Texture2D>
        {
            { "Punch", LoadTexture("Content/Icons/Punch.png") },
            { "Throw Stone", LoadTexture("Content/Icons/StoneThrow.png") },
            { "Shout", LoadTexture("Content/Icons/Shout.png") }
        };

        var map = new EncounterMapGenerator().Generate(BiomeType.Plains, seed: 42);
        _encounterState = new EncounterState(map);

        var merc1 = new Mercenary(stamina: 6);
        var merc2 = new Mercenary(stamina: 6);
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
        _scene = new EncounterScene(_encounterState, _turnManager, _pixel, resolver, _skillIcons);
        _ai = new EnemyAI(resolver);
    }

    protected override void Update(GameTime gameTime)
    {
        _scene.Update(Mouse.GetState());

        if (_turnManager.CurrentCreature is not Mercenary && _turnManager.CanMove(_turnManager.CurrentCreature!))
        {
            var enemy = _turnManager.CurrentCreature!;
            _ai.TakeTurn(enemy, _encounterState);
            _turnManager.EndCreatureTurn(enemy);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin();
        _scene.Draw(_spriteBatch);
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private Texture2D LoadTexture(string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            return Texture2D.FromStream(GraphicsDevice, stream);
        }
    }
}
