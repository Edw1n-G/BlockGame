using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Basics.Window;
using Egui;
using Basics.Game.Logic;
using Egui.Containers;
using Egui.Silk.NET;
using StbImageSharp;

namespace Basics.EngineStates;

public class Menu : IStates
{
    private GL _gl;
    private IInputContext  _inputContext;
    private StateManager _manager;
    
    private Context _uiContext = null!;
    private Basics.SilkGlIntegration _uiIntegration = null!;
    
    private bool _showSettings = false;
    
    public void Enter(GL gl, IInputContext inputContext, StateManager manager)
    {
        _gl = gl;
        _inputContext = inputContext;
        _manager = manager;
        
        //Mauszeiger anzeigen. idk kann man mehrere mäuse benutzen in windows?
        foreach (var mouse in _inputContext.Mice)
        {
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }
        
        //Egui.NET
        _uiContext = new Context();
        _uiIntegration = new Basics.SilkGlIntegration(_uiContext, WindowSetup.Window, _inputContext);
    }

    public void Update(double delta)
    {
        
    }

    public void Render(double delta)
    {
        _gl.ClearColor(0.1f, 0.15f, 0.2f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        
        _uiIntegration.Run(ctx => Draw(ctx));
    }

    public void FramebufferResize(Vector2D<int> newSize)
    {
        
    }

    public void Exit()
    {
        Console.WriteLine("Exiting Menu State");
        _uiIntegration?.Dispose();
        _uiIntegration = null;
        _uiContext = null;
    }
    
    
    public void Draw(Context ctx)
    {
        new Area("MainMenuArea")
            .Anchor(Align2.CenterCenter, new EVec2(0, 0))
            .Show(ctx, ui =>
            {
                var backgroundFrame = new Frame
                {
                    Fill = Color32.DarkBlue,
                    CornerRadius = new CornerRadius { Nw = 10, Ne = 10, Sw = 10, Se = 10 }
                };

                backgroundFrame.Show(ui, frameUi =>
                {
                    // Titel
                    frameUi.Heading("MeinKraft");
                    frameUi.Label("Version 0.12.0 Alpha");
                    
                    // Einen kleinen Strich als Trenner
                    frameUi.Separator();
                    
                    if (frameUi.Button("Spiel starten").Clicked)
                    {
                        _manager.StateChange(new Game());
                    }

                    if (frameUi.Button("Einstellungen").Clicked)
                    {
                        _showSettings = !_showSettings; 
                    }

                    if (frameUi.Button("Beenden").Clicked)
                    {
                        _manager.CloseEngine(); 
                    }
                });
            });
        
        if (_showSettings)
        {
            new Egui.Containers.Window("Menü Einstellungen")
                .Resizable((false, false))
                .Show(ctx, ui =>
                {
                    int dist = GameSettings.RenderDistance;
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref dist, 1, 40).Text("Render Distance")).Changed)
                    {
                        GameSettings.SetRenderDistance(dist);
                    }
                    
                    int size = GameSettings.MapSize;
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref size, 1, 500).Text("World size (in Chunks)")).Changed)
                    {
                        GameSettings.SetMapSize(size);
                    }
                    
                    if (ui.Button("Schließen").Clicked)
                    {
                        _showSettings = false;
                    }
                });
        }
        
        new Egui.Containers.Area("Controlls")
                .Anchor(Align2.RightBottom, new EVec2(20, 10))
                .Show(ctx, ui =>
                {
                    // Einen Frame definieren. Hier kannst du Farbe, Rundungen und Abstände (Margin) anpassen.
                    var backgroundFrame = new Egui.Containers.Frame
                    {
                        Fill = Color32.Black,
                        CornerRadius = new CornerRadius { Nw = 5, Ne = 5, Sw = 5, Se = 5 }
                    };

                    // Den Frame in das 'ui' der Area zeichnen. 
                    backgroundFrame.Show(ui, frameUi =>
                    {
                        frameUi.Label("WASD Shift Space - Movement");
                        frameUi.Label("F - Mouse Toggle");
                        frameUi.Label("F1 - Debug Cam (No worki)");
                        frameUi.Label("F11 - Fullscreen");
                        frameUi.Label("F12 - Borderless Fullscreen");
                    });
                });

        new Egui.Containers.Area("Pfp")
            .Anchor(Align2.RightTop, new EVec2(10, 10))
            .Show(ctx, ui =>
            {
                ui.Add(new Egui.Widgets.Image(EguiHelpers.IncludeImageResource("Core.images.me.png"))
                    .FitToExactSize((128f, 128f)));
            });
    }
}