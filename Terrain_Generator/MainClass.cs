using System.Numerics;
using Basics.Configurations;
using Basics.Game;
using Basics.Game.TerrainManaging;
using Silk.NET.Input; //Für die Tastatureingabe
using Silk.NET.Maths; //Für die Vector2D Klasse
using Basics.Graphics;
using Basics.Setup;
using Basics.Input;
using Basics.Utilities;
using Silk.NET.Windowing;

namespace Basics;

/**
 * Verwaltung der Logic und Render Loops
 * Vorerst auch Fenster Skalierung
 */
public class MainClass
{
    private static Renderer PlayerRenderer;
    private static TerrainGenerator _terrainGenerator;
    private static ChunkProvider _chunkProvider;
    private static ChunkRequestor _chunkRequestor;

    public static Camera PlayerCamera;
    public static Camera? DebugCamera; // Zweite Freecam 
    private static Vector3 PlayerStartPosition = new Vector3(0, 40, 0);
    
    /**
     * Startpunkt des Programms
     * Fenster erstellen und Events registrieren
     */
    public void Run()
    {
        //Fenster erstellen und die Event-Handler registrieren
        WindowSetup.CreateWindow();
        
        WindowSetup.window.Load += OnLoad;
        WindowSetup.window.Render += OnRender;
        WindowSetup.window.Update += OnUpdate;
        WindowSetup.window.FramebufferResize += OnFramebufferResize;
        
        //Fenster starten und Haupt-thread
        WindowSetup.Run();
        
        WindowSetup.window.Dispose();
    }
    
    /**
     * Nach erstellen des Fensters Renderer und InputManager initialisieren
     */
    private unsafe void OnLoad()
    {   
        //Kerne aufteilen und reservieren
        CoreAvailability.Initialize();
        
        //Main Camera und Renderer erstellen
        PlayerRenderer = new Renderer();
        PlayerCamera = new Camera(PlayerStartPosition);
        PlayerRenderer.Setup(PlayerCamera);
        
        //Main Camera an die Movement Klasse geben
        Movement.SetPlayerCamera(PlayerCamera);
        
        //Die Inputs vom Fenster an den InputManager weitergeben
        IInputContext input = WindowSetup.window.CreateInput();
        InputManager.Initialize(input);
        
        //Aktion -> Methode Mappen
        InputManager.SetActionBindings(Actions.Close, () => WindowSetup.window.Close());
        InputManager.SetActionBindings(Actions.Fullscreen, ToggleFullscreen);
        InputManager.SetActionBindings(Actions.Borderless, ToggleBorderless);
        InputManager.SetActionBindings(Actions.ToogleDebugCamera, ToggleDebugCamera);
        
        // Texture Mapping lesen und in den speicher legen
        BlockTextures.Initialize("Configurations/TextureConfig.json");
        
        // Terrain-Pipeline aufbauen:
        // TerrainGenerator erzeugt Chunk-Daten
        _terrainGenerator = new TerrainGenerator();
        _terrainGenerator.SetMapSize(32);
        
        // ChunkProvider verwaltet den Chunk-Lebenszyklus (Laden/Generieren/Speichern)
        _chunkProvider = new ChunkProvider(_terrainGenerator);
        Renderer.ChunkProvider = _chunkProvider;
        
        // ChunkRequestor abonniert das Camera-Event und berechnet welche Chunks geladen werden
        // Die Chunks werden dann vom Provider parallel bereitgestellt
        int generationCores = CoreAvailability.GetTerrainGenerationCores();
        _chunkRequestor = new ChunkRequestor(PlayerCamera, _chunkProvider, generationCores);
        
        // Initiales Laden der Chunks um die Startposition
        PlayerCamera.ForceChunkUpdate();
        
        //_terrainGenerator.DebugExportNoiseMap();
    }

//Wird jeden Frame aufgerufen, hier wird alles gerendert.
    private static unsafe void OnRender(double deltaTime)
    {
        PlayerRenderer.Clear();//Vorherigen Frame löschen
        PlayerRenderer.Render();
    }
    
    //Wird jeden Frame aufgerufen, hier wird alles außer dem Rendering gemacht.
    private static void OnUpdate(double deltaTime)
    {
        Movement.MovementUpdate(deltaTime);
    }
    
    //Wird aufgerufen, wenn die Fenstergröße geändert wird.
    private static void OnFramebufferResize(Vector2D<int> newSize)
    {
        PlayerRenderer.FramebufferResize(newSize);
    }
    
    //========================================================================
    //Muss noch in eine dedizierte Klasse für Einstellungen/Sachen die nicht direkt mit dem Spiel zu tun haben
    //========================================================================
    
    //Funktionen die durch Tasten getriggert werden können.
    private static void ToggleFullscreen()
    {   
        if (WindowSetup.window.WindowState == WindowState.Fullscreen)
        {
            // Raus aus Vollbild → Zurück zum normalen Fenster
            WindowSetup.window.WindowState = WindowState.Normal;
            WindowSetup.window.WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            // Bevor wir in den Fullscreen gehen, setzen wir das Fenster in 
            // einen sauberen Grundzustand. So merkt sich das System keine "falschen"
            // Borderless-Eigenschaften, die später zu Glitches führen.
            WindowSetup.window.WindowState = WindowState.Normal;
            WindowSetup.window.WindowBorder = WindowBorder.Resizable;
        
            // Jetzt sicher in den Vollbildmodus wechseln
            WindowSetup.window.WindowState = WindowState.Fullscreen;
        }
    }

    private static void ToggleBorderless()
    {
        // FIX 2: Hier das logische ODER (||) nutzen
        if (WindowSetup.window.WindowBorder == WindowBorder.Hidden || WindowSetup.window.WindowState == WindowState.Fullscreen)
        {
            // Zurück zum normalen kleinen Fenster MIT Rahmen
            WindowSetup.window.WindowState = WindowState.Normal;
            WindowSetup.window.WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            // Rein in den Borderless-Windowed Modus
            WindowSetup.window.WindowState = WindowState.Normal;   // Zuerst ent-maximieren
            WindowSetup.window.WindowBorder = WindowBorder.Hidden; // Dann Rahmen ausblenden
            WindowSetup.window.WindowState = WindowState.Maximized;// Dann über den Bildschirm strecken
        }
    }
    
    // Debug-Camera toggeln
    private static void ToggleDebugCamera()
    {
        if (DebugCamera == null)
        {
            // Debug-Camera erstellen und aktivieren
            DebugCamera = new Camera(PlayerCamera.Position);
            DebugCamera.Front = PlayerCamera.Front;
            DebugCamera.Pitch = PlayerCamera.Pitch;
            DebugCamera.Yaw = PlayerCamera.Yaw;
            Movement.SetPlayerCamera(DebugCamera);
            Console.WriteLine("Debug Camera aktiviert");
        }
        else
        {
            // Zurück zur Player-Camera wechseln und Debug-Camera löschen
            Movement.SetPlayerCamera(PlayerCamera);
            DebugCamera = null;
            Console.WriteLine("Debug Camera deaktiviert");
        }
    }
}