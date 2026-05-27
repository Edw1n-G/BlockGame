using System.Numerics;
using Basics.Configurations;
using Basics.Game;
using Basics.Game.Graphics;
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
    StateManager _manager;
    
    //Spielwelt und Renderer
    private static Renderer _playerRenderer = null!;
    private static TerrainGenerator _terrainGenerator  = null!;
    private static ChunkProvider _chunkProvider = null!;
    private static ChunkRequestor _chunkRequestor = null!;
    public static float Fps = 0;
    
    private static GL _gl = null!;

    // Spieler
    private static PlayerCharacter _player = null!;
    private static PlayerMovement _playerMovement = null!;
    public static Camera PlayerCamera  = null!;
    public static Camera? DebugCamera  = null!; // Zweite Free cam
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
        
        //Kerne aufteilen und reservieren
        CoreAvailability.Initialize();

        // Datengetriebene Block- und Textur-Registries laden.
        BlockLoader.Initialize(_gl, "Content");
        
        // Player mit eigener Kamera erstellen
        _player = new PlayerCharacter(PlayerStartPosition);
        PlayerCamera = _player.Camera;

        //Main Camera und Renderer erstellen
        _playerRenderer = new Renderer();
        _playerRenderer.Setup(PlayerCamera, _gl);

        //Movement-Controller für Player/Debug Kamera
        _playerMovement = new PlayerMovement(_player);

        //Egui.NET
        _uiContext = new Context();
        _uiIntegration = new SilkGlIntegration(_uiContext, WindowSetup.Window, inputContext);
        
        //Input Manager
        InputManager.Initialize(inputContext);
        InputManager.SetPlayerMovement(_playerMovement); //TODO: InputManager vom Player trennen

        InputManager.SetActionBindings(Actions.ToogleDebugCamera, ToggleDebugCamera);
        
        //Multithreading System initializieren
        var jobScheduler = new JobScheduler();
        
        // Terrain-Pipeline aufbauen:
        // TerrainGenerator erzeugt Chunk-Daten
        _terrainGenerator = new TerrainGenerator();
        
        // ChunkProvider verwaltet den Chunk-Lebenszyklus (Laden/Generieren/Speichern)
        _chunkProvider = new ChunkProvider(jobScheduler);
        Renderer.ChunkProvider = _chunkProvider;
        
        // ChunkRequestor abonniert das Player-Event und berechnet welche Chunks geladen werden
        // die Chunks werden dann vom Provider parallel erstellt und verwaltet
        _chunkRequestor = new ChunkRequestor(_player, _chunkProvider);
        
        //Schedular starten mit context
        var context = new JobContext(_terrainGenerator, _chunkProvider);
        jobScheduler.Start(CoreAvailability.GetTaskCores(), context);
        
        //UI erstellen
        _uiManager = new UIManager(_chunkRequestor);
        
        //====================================================================
        //Nachdem Alle Objekte da sind globale ReferenzPunkte setzen, wo nötig
        World.Initialize(_chunkProvider);
        Physics.Initialize(new Raycaster(_chunkProvider));
        //====================================================================

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
        //TODO: alles aus dem vram und ram löschen
        // ChunkProvidorlisten und texturen
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