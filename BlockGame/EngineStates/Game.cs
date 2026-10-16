using System.Numerics;
using Basics.Game.Configurations;
using Basics.Game;
using Basics.Game.Graphics;
using Basics.Game.Logic;
using Basics.Game.Logic.Player;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;
using Basics.Game.PhysicsSystem;
using Basics.Game.Player;
using Basics.Game.Utilities;
using Basics.Graphics.UI;
using Basics.Input;
using Basics.Window;
using Egui;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.EngineStates;

/**
 * Verwaltung der Logic und Render Loops
 * vorerst auch Fenster Skalierung
 */
public class Game : IStates
{
    private StateManager _manager;
    private EngineContext _context = null!;
    
    //Spielwelt und Renderer
    private static Renderer _playerRenderer = null!;
    private static TerrainGenerator _terrainGenerator = null!;
    private static ChunkProvider _chunkProvider = null!;
    private static ChunkRequestor _chunkRequestor = null!;
    public static float Fps = 0;//wird in der UI benutzt
    
    private static GL _gl = null!;

    // Spieler
    private static PlayerCharacter _player = null!;
    private static PlayerMovement _playerMovement = null!;
    public static Camera PlayerCamera = null!;
    public static Camera? DebugCamera = null!; // Zweite Free cam
    private static readonly Vector3 PlayerStartPosition = new Vector3(0, 100, 0);
    
    //Ingame UI
    private static UIManager _uiManager = null!; // Das was die UI elemente definiert
    private static Context _uiContext = null!;
    private static SilkIntegration _uiIntegration = null!; // Verbindet Egui mit dem Silk.NET Fenster und zieht sich alle events
    
    /**
     * Nach Erstellen des Fensters Renderer und InputManager initialisieren
     */
    public unsafe void Enter(GL gl, IInputContext inputContext, StateManager manager)
    {   
        //OpenGl local speichern
        _gl = gl;
        _manager = manager;

        _context = new EngineContext(settings: GameSettings.Current);
        EngineContext.SetGlobal(_context);
        
        //Kerne aufteilen und reservieren
        _context.CoreAvailability.InitializeInstance();
        _context.BlockRegistry.Initialize(_gl, "Content");
        
        // Subsystem-Referenzen aus dem Kontext beziehen
        _chunkProvider = _context.ChunkProvider;
        _terrainGenerator = _context.TerrainGenerator;

        // Spieler mit eigener Kamera erstellen (injizierte Subsysteme)
        _player = new PlayerCharacter(PlayerStartPosition, _context.World, _context.Physics);
        PlayerCamera = _player.Camera;

        //Main Camera und Renderer erstellen
        _playerRenderer = new Renderer(_chunkProvider);
        _playerRenderer.Setup(PlayerCamera, _gl);

        //Movement-Controller für Player/Debug Kamera
        _playerMovement = new PlayerMovement(_player, _context.Settings);

        //Egui.NET
        _uiContext = new Context();
        _uiIntegration = new SilkGlIntegration(_uiContext, WindowSetup.Window, inputContext);
        
        //Input Manager
        InputManager.Initialize(inputContext);
        InputManager.SetPlayerMovement(_playerMovement); //TODO: InputManager vom Player trennen
        InputManager.SetActionBindings(Actions.ToogleDebugCamera, ToggleDebugCamera);
        
        // ChunkRequestor abonniert das Player-Event und berechnet welche Chunks geladen werden
        _chunkRequestor = new ChunkRequestor(_player, _chunkProvider, _context.Settings);
        
        //Schedular starten mit context
        var jobContext = new JobContext(_terrainGenerator, _chunkProvider, _context.BlockRegistry);
        _context.JobScheduler.Start(_context.CoreAvailability.GetCores(), jobContext);
        
        //UI erstellen
        _uiManager = new UIManager(_chunkRequestor);

        // Initiales Laden der Chunks um die Startposition
        _player.ForceChunkUpdate();

        //_terrainGenerator.DebugExportNoiseMap();
    }

    //Wird jeden Frame aufgerufen, hier wird alles gerendert.
    public unsafe void Render(double deltaTime)
    {
        Fps = (float)(1.0 / deltaTime);
        //SpieleWelt rendern
        _playerRenderer.Clear();//Vorherigen Frame löschen
        _playerRenderer.Render();
        
        //UI rendern
        _gl.Disable(EnableCap.DepthTest);// Die UI soll immer sichtbar sein. wird in shader.Use() wieder aktiviert
        
        _uiIntegration.Run(ctx => _uiManager.Draw(ctx));
    }
    
    //Wird jeden Frame aufgerufen, hier wird alles außer dem Rendering gemacht.
    public void Update(double deltaTime)
    {
        _playerMovement.MovementUpdate(deltaTime);
        _chunkProvider.RequestMeshes();
    }
    
    //Wird aufgerufen, wenn die Fenstergröße geändert wird.
    public void FramebufferResize(Vector2D<int> newSize)
    {
        _playerRenderer.FramebufferResize(newSize);
    }

    public void Exit()
    {
        _context?.Dispose();
    }
    
    // Debug-Camera toggeln
    private static void ToggleDebugCamera()
    {
        if (DebugCamera == null)
        {
            // Debug-Camera erstellen und aktivieren
            DebugCamera = new Camera(PlayerCamera.Position)
            {
                Front = PlayerCamera.Front,
                Pitch = PlayerCamera.Pitch,
                Yaw = PlayerCamera.Yaw
            };
            _playerMovement.SetActiveCamera(DebugCamera);
            _playerRenderer.SetCamera(DebugCamera);
            Console.WriteLine("Debug Camera aktiviert");
        }
        else
        {
            // Zurück zur Player-Camera wechseln und Debug-Camera löschen
            _playerMovement.UsePlayerCamera();
            _playerRenderer.SetCamera(PlayerCamera);
            DebugCamera = null;
            Console.WriteLine("Debug Camera deaktiviert");
        }
    }
}