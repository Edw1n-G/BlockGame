using Basics.Game;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Basics.Utilities;
using Basics.Window;
using Egui;
using Egui.Containers;
using Egui.Silk.NET;

namespace Basics.EngineStates;

public class Menu : IStates
{
    private GL _gl;
    private IInputContext  _inputContext;
    private StateManager _manager;
    
    private static Context _uiContext = null!;
    private static SilkIntegration _uiIntegration = null!;
    
    private bool _showSettings = false;
    
    public void Enter(GL gl, IInputContext inputContext, StateManager manager)
    {
        _gl = gl;
        _inputContext = inputContext;
        _manager = manager;

        //Mauszeiger anzeigen
        foreach (var mouse in _inputContext.Mice)
        {
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }
        
        //Egui.NET
        _uiContext = new Context();
        _uiIntegration = new SilkGlIntegration(_uiContext, WindowSetup.Window, _inputContext);
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
                    frameUi.Label("Version 0.9 Alpha");
                    
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
                    
                    if (ui.Button("Schließen").Clicked)
                    {
                        _showSettings = false;
                    }
                });
        }
    }
}