using System.Numerics;
using Basics.Configurations;
using Basics.Game;
using Basics.Game.Graphics;
using Basics.Game.Player;
using Basics.Game.TerrainManaging;
using Basics.Game.TerrainManaging.Generation;
using Basics.Graphics;
using Basics.Graphics.UI;
using Basics.Input;
using Basics.PhysicsSystem;
using Basics.Utilities;
using Basics.Window;
using Egui;
using Egui.Silk.NET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
//Für die Tastatureingabe
//Für die Vector2D Klasse
// Für die Egui-Integration mit Silk.NET
// Für die Egui-UI-Komponenten

namespace Basics.EngineStates;

/**
 * Verwaltung der Logic und Render Loops
 * vorerst auch Fenster Skalierung
 */
public class Game
{
    //Spielwelt und Renderer
    private static Renderer _playerRenderer = null!;
    private static TerrainGenerator _terrainGenerator  = null!;
    private static ChunkProvider _chunkProvider = null!;
    private static ChunkRequestor _chunkRequestor = null!;
    private static GL _gl = null!;

    // Spieler
    private static PlayerCharacter _player = null!;
    private static PlayerMovement _playerMovement = null!;
    public static Camera PlayerCamera  = null!;
    public static Camera? DebugCamera  = null!; // Zweite Freecam
    private static readonly Vector3 PlayerStartPosition = new Vector3(0, 40, 0);
    
    //Ingame UI
    private static UIManager _uiManager = null!; // Das was die UI elemte definiert
    private static Context _uiContext = null!; // IDK was das bedeuten soll
    private static SilkIntegration _uiIntegration = null!; // Verbindet Egui mit dem Silk.NET Fenster und zieht sich alle events
    
    /**
     * Startpunkt des Programms
     * Fenster erstellen und Events registrieren
     */
    public void Run()
    {
        //Fenster erstellen und die Event-Handler registrieren
        WindowSetup.CreateWindow();
        
        WindowSetup.Window.Load += OnLoad;
        WindowSetup.Window.Render += OnRender;
        WindowSetup.Window.Update += OnUpdate;
        WindowSetup.Window.FramebufferResize += OnFramebufferResize;
        
        //Fenster starten und Haupt-thread
        WindowSetup.Run();
        
        WindowSetup.Window.Dispose();
    }
    
    /**
     * Nach Erstellen des Fensters Renderer und InputManager initialisieren
     */
    private unsafe void OnLoad()
    {   
        //OpenGl erstellen
        _gl = WindowSetup.Window.CreateOpenGL();
        
        //Kerne aufteilen und reservieren
        CoreAvailability.Initialize();
        
        // Player mit eigener Kamera erstellen
        _player = new PlayerCharacter(PlayerStartPosition);
        PlayerCamera = _player.Camera;

        //Main Camera und Renderer erstellen
        _playerRenderer = new Renderer();
        _playerRenderer.Setup(PlayerCamera, _gl);

        //Movement-Controller für Player/Debug Kamera
        _playerMovement = new PlayerMovement(_player);

        //Creating Input Context
        IInputContext input = WindowSetup.Window.CreateInput();
        
        //Egui.NET
        _uiContext = new Context();
        _uiIntegration = new SilkGlIntegration(_uiContext, WindowSetup.Window, input);
        
        //Input Manager
        InputManager.Initialize(input);
        InputManager.SetPlayerMovement(_playerMovement); //TODO: InputManager vom Player trennen

        InputManager.SetActionBindings(Actions.ToogleDebugCamera, ToggleDebugCamera);
        
        // Texture Mapping lesen und in den speicher legen
        BlockTextures.Initialize("Game/Configurations/TextureConfig.json");
        
        // Terrain-Pipeline aufbauen:
        // TerrainGenerator erzeugt Chunk-Daten
        _terrainGenerator = new TerrainGenerator();
        
        // ChunkProvider verwaltet den Chunk-Lebenszyklus (Laden/Generieren/Speichern)
        int meshingThreads = CoreAvailability.GetChunkMeshingCores();
        _chunkProvider = new ChunkProvider(_terrainGenerator, meshingThreads );
        Renderer.ChunkProvider = _chunkProvider;
        
        // ChunkRequestor abonniert das Player-Event und berechnet welche Chunks geladen werden
        // Die Chunks werden dann vom Provider parallel erstellt und verwaltet
        int generationCores = CoreAvailability.GetTerrainGenerationCores();
        _chunkRequestor = new ChunkRequestor(_player, _chunkProvider, generationCores);
        
        //UI erstellen
        _uiManager = new UIManager(_chunkRequestor);
        
        //====================================================================
        //Nachdem Alle Objekte Da sind globale ReferenzPunkte setzen wo nötig
        World.Initialize(_chunkProvider);
        Physics.Initialize(new Raycaster(_chunkProvider));
        //====================================================================

        // Initiales Laden der Chunks um die Startposition
        _player.ForceChunkUpdate();

        //_terrainGenerator.DebugExportNoiseMap();
    }

//Wird jeden Frame aufgerufen, hier wird alles gerendert.
    private static unsafe void OnRender(double deltaTime)
    {
        //SpieleWelt rendern
        _playerRenderer.Clear();//Vorherigen Frame löschen
        _playerRenderer.Render();
        
        //UI rendern
        _gl.Disable(EnableCap.DepthTest);// Die UI soll immer sichtbar sein. wird in shader.Use() wieder aktiviert
        
        _uiIntegration.Run(ctx => _uiManager.Draw(ctx));
    }
    
    //Wird jeden Frame aufgerufen, hier wird alles außer dem Rendering gemacht.
    private static void OnUpdate(double deltaTime)
    {
        _playerMovement.MovementUpdate(deltaTime);
    }
    
    //Wird aufgerufen, wenn die Fenstergröße geändert wird.
    private static void OnFramebufferResize(Vector2D<int> newSize)
    {
        _playerRenderer.FramebufferResize(newSize);
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