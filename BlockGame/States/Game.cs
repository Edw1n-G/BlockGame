using Basics.Blocks;
using Basics.Common;
using Basics.Engine;
using Basics.Terrain.Generation;
using Basics.Graphics;
using Basics.Graphics.Chunks;
using Basics.Graphics.UI;
using Basics.Input;
using Basics.Jobs;
using Basics.PhysicsSystem;
using Basics.Player;
using Basics.Settings;
using Basics.Terrain;
using Basics.Terrain.Chunks;
using Basics.Terrain.Selection;
using Basics.Textures;
using Basics.Window;
using System.Numerics;
using Egui;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.States;

/**
 * Verwaltung der Logic und Render Loops
 * vorerst auch Fenster Skalierung
 */
public class Game : IStates
{
    private StateManager _manager;
    private long _fpsStart = System.Diagnostics.Stopwatch.GetTimestamp();
    private int _fpsFrames;
    public static double FrameMilliseconds;
    private Basics.Diagnostics.FrameBenchmarkRun? _benchmark;
    private EngineContext _context = null!;
    private TextureArray? _terrainTexture;
    
    //Spielwelt und Renderer
    private static Renderer _playerRenderer = null!;
    private static TerrainGenerator _terrainGenerator = null!;
    private static ChunkProvider _chunkProvider = null!;
    private static ChunkRequestor _chunkRequestor = null!;
    public static float Fps = 0;//wird in der UI benutzt
    
    private static GL _gl = null!;
    
    // Input
    private static InputManager _inputManager = null!;

    // Spieler
    private static PlayerCharacter _player = null!;
    private static PlayerMovement _playerMovement = null!;
    public static Camera PlayerCamera = null!;
    public static Camera? DebugCamera = null!; // Zweite Free cam
    private static readonly Vector3 PlayerStartPosition = new Vector3(0, 100, 0);
    
    //Ingame UI
    private static UIManager _uiManager = null!; // Das was die UI elemente definiert
    private static Context _uiContext = null!;
    private static GameEguiIntegration _uiIntegration = null!; // Verbindet Egui mit dem Silk.NET Fenster und zieht sich alle events
    
    /**
     * Nach Erstellen des Fensters Renderer und InputManager initialisieren
     */
    public unsafe void Enter(GL gl, IInputContext inputContext, InputManager inputManager, StateManager manager)
    {   
        //OpenGl local speichern
        _gl = gl;
        _manager = manager;
        _inputManager = inputManager;
        _inputManager.LockMouse();
        if (Basics.Diagnostics.FrameBenchmarkRun.Options == null)
            _inputManager.SetActionBindings(Actions.ToggleDebugCamera, ToggleDebugCamera);

        _context = new EngineContext(new GlChunkMeshFactory(gl), settings: GameSettings.Current);
        EngineContext.SetGlobal(_context);
        
        //Kerne aufteilen und reservieren
        _context.CoreAvailability.InitializeInstance();
        _terrainTexture = BlockTextureLoader.Load(_gl, _context.BlockRegistry);
        
        // Subsystem-Referenzen aus dem Kontext beziehen
        _chunkProvider = _context.ChunkProvider;
        _terrainGenerator = _context.TerrainGenerator;
        
        // Spieler mit eigener Kamera erstellen (injizierte Subsysteme)
        _player = new PlayerCharacter(Basics.Diagnostics.FrameBenchmarkRun.Options != null ? new Vector3(0, 150, 0) : PlayerStartPosition, _inputManager, _context.World, _context.Physics);
        PlayerCamera = _player.Camera;
        if (Basics.Diagnostics.FrameBenchmarkRun.Options != null) PlayerCamera.Rotate(0, 45);

        //Main Camera und Renderer erstellen
        _playerRenderer = new Renderer(_chunkProvider);
        _playerRenderer.Setup(PlayerCamera, _gl, _terrainTexture);

        //Movement-Controller für Player/Debug Kamera
        if (Basics.Diagnostics.FrameBenchmarkRun.Options is { } options)
        {
            _benchmark = new(options, _context.Settings, $"{gl.GetStringS(StringName.Vendor)} / {gl.GetStringS(StringName.Renderer)} / {gl.GetStringS(StringName.Version)}");
            _playerMovement = new PlayerMovement(_player, _benchmark.Input, _context.Settings);
            _inputManager.SetActionBindings(Actions.DestroyBlock, () => { });
            _inputManager.SetActionBindings(Actions.PlaceBlock, () => { });
            _inputManager.SetActionBindings(Actions.Fullscreen, () => { });
            _inputManager.SetActionBindings(Actions.Borderless, () => { });
        }
        else _playerMovement = new PlayerMovement(_player, _inputManager, _context.Settings);

        //Egui.NET
        _uiContext = new Context();
        _uiIntegration = new GameEguiIntegration(_uiContext, WindowSetup.Window, inputContext, _inputManager);
        
        // ChunkRequestor abonniert das Player-Event und berechnet welche Chunks geladen werden
        _chunkRequestor = new ChunkRequestor(_player, _chunkProvider, _context.Settings);
        
        //Schedular starten mit context
        var jobContext = new JobContext(_terrainGenerator, _chunkProvider, _context.BlockRegistry);
        _context.JobScheduler.Start(_context.CoreAvailability.GetCores(), jobContext);
        
        //UI erstellen
        _uiManager = new UIManager(_chunkRequestor);

        // Initiales Laden der Chunks um die Startposition
        _player.ForceChunkUpdate();

    }

    //Wird jeden Frame aufgerufen, hier wird alles gerendert.
    public unsafe void Render(double deltaTime)
    {
        long renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_benchmark != null) _playerRenderer.CoverageRegion = _benchmark.Region(PlayerCamera);
        _fpsFrames++;
        double fpsSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(_fpsStart).TotalSeconds;
        if (fpsSeconds >= 1)
        {
            Fps = (float)(_fpsFrames / fpsSeconds);
            FrameMilliseconds = fpsSeconds * 1000 / _fpsFrames;
            _fpsFrames = 0; _fpsStart = renderStart;
        }
        //SpieleWelt rendern
        _playerRenderer.Clear();//Vorherigen Frame löschen
        _playerRenderer.Render();
        
        //UI rendern
        _gl.Disable(EnableCap.DepthTest);// Die UI soll immer sichtbar sein. wird in shader.Use() wieder aktiviert
        
        _uiIntegration.Run(ctx => _uiManager.Draw(ctx));
        _benchmark?.EndFrame(deltaTime, System.Diagnostics.Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds,
            _chunkProvider, _playerRenderer.TargetResident, _playerRenderer.TargetDrawn, _chunkRequestor.LastUpdateFailure != null, !_chunkRequestor.IsUpdating);
        if (_benchmark?.Finished == true) WindowSetup.Window.Close();
    }
    
    //Wird jeden Frame aufgerufen, hier wird alles außer dem Rendering gemacht.
    public void Update(double deltaTime)
    {
        if (_benchmark == null) _playerMovement.MovementUpdate(deltaTime);
        else _benchmark.Update(_playerMovement, PlayerCamera);
        _chunkProvider.RequestMeshes();
    }
    
    //Wird aufgerufen, wenn die Fenstergröße geändert wird.
    public void FramebufferResize(Vector2D<int> newSize)
    {
        _playerRenderer.FramebufferResize(newSize);
    }

    public void Exit()
    {
        _benchmark?.Abort(_chunkProvider);
        _uiIntegration?.Dispose();
        _playerMovement?.Dispose();
        _player?.Dispose();
        if (_chunkRequestor != null && !_chunkRequestor.Stop()) return;
        if (_context?.Shutdown() == true) _terrainTexture?.Dispose();
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
