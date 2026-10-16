using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;
using Egui;

namespace Basics.Graphics.UI
{
    /// <summary>
    /// Verwaltet die Egui-basierte Benutzeroberfläche (UI).
    /// Zeichnet UI-Elemente wie Kameraansicht, Settings-Fenster, Spielerinformationen und Render-Distance-Steuerung.
    /// </summary>
    public class UIManager
    {
        private ChunkRequestor _chunkRequestor;
        private readonly GameSettings _settings;
        
        /// <summary>
        /// Ereignis, das ausgelöst wird, wenn sich die Render-Distance geändert hat.
        /// </summary>
        public static event Action OnRenderDistanceChanged;

        /// <summary>
        /// Initialisiert eine neue Instanz des <see cref="UIManager"/>-Klassens.
        /// </summary>
        /// <param name="requestor">Der ChunkRequestor, der zum Entladen von Chunks verwendet wird.</param>
        /// <param name="settings">Optional die zu verwendenden GameSettings. Wenn nicht angegeben, werden die aktuellen Settings verwendet.</param>
        public UIManager(ChunkRequestor requestor, GameSettings? settings = null)
        {
            _chunkRequestor = requestor;
            _settings = settings ?? EngineContext.Current?.Settings ?? GameSettings.Current;
        }

        /// <summary>
        /// Zeichnet alle UI-Elemente.
        /// Wird aus dem Egui-Integration.Run()-Methoden aufgerufen.
        /// </summary>
        /// <param name="ctx">Der Egui-Kontext für das Zeichnen.</param>
        public void Draw(Context ctx)
        {
            var style = ctx.Style;
            var visuals = style.Visuals;
            visuals.WindowShadow = Shadow.None;
            visuals.PopupShadow = Shadow.None;
            style.Visuals = visuals;
            ctx.SetStyle(style);
            
            new Egui.Containers.Area("crosshair")
                .Anchor(Align2.CenterCenter, new EVec2(0, 0))
                .Show(ctx, ui => { ui.Label("+"); });
        
            new Egui.Containers.Window("Engine Settings")
                .Resizable((false, false))
                .Show(ctx, ui =>
                {
                    int hdist = _settings.RenderDistance;
                    int vdist = _settings.VerticalRenderDistance;
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref hdist, 1, 60).Text("Horizontal Render Distance")).Changed)
                    {
                        _settings.SetRenderDistance(hdist);
                        OnRenderDistanceChanged?.Invoke();
                    }
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref vdist, 1, 30).Text("Vertical Render Distance")).Changed)
                    {
                        _settings.SetVerticalRenderDistance(vdist);
                        OnRenderDistanceChanged?.Invoke();
                    }
                    if (ui.Button("Unload All Chunks").Clicked)
                    {
                        _chunkRequestor.UnloadAllChunks();
                    }
                });
            
            new Egui.Containers.Window("PlayerSettings")
                .Resizable((false, false))
                .Show(ctx, ui =>
                {
                    ui.Heading("Player Settings");
                    float speed = _settings.PlayerMoveSpeed;
                    if (ui.Add(new Egui.Widgets.Slider<float>(ref speed, 1f, 100f).Text("Player Speed")).Changed)
                    {
                        _settings.SetPlayerMoveSpeed(speed);
                    }
                    
                    float sensitivity = _settings.MouseSensitivity;
                    if (ui.Add(new Egui.Widgets.Slider<float>(ref sensitivity, 0.01f, 1f).Text("Mouse Sensitivity")).Changed)
                    {
                        _settings.SetMouseSensitivity(sensitivity);
                    }
                });
            
            new Egui.Containers.Area("PlayerInformation")
                .Anchor(Align2.RightTop, new EVec2(-10, 10))
                .Show(ctx, ui =>
                {
                    var backgroundFrame = new Egui.Containers.Frame
                    {
                        Fill = Color32.Black,
                        CornerRadius = new CornerRadius { Nw = 5, Ne = 5, Sw = 5, Se = 5 }
                    };

                    backgroundFrame.Show(ui, frameUi =>
                    {
                        frameUi.Heading("Player Information");

                        new Egui.Grid("player_info_grid")
                            .Striped(true)
                            .Show(frameUi, gridUi =>
                            {
                                gridUi.Label("X:");
                                gridUi.Label(EngineStates.Game.PlayerCamera.Position.X.ToString("0.00"));
                                gridUi.EndRow();

                                gridUi.Label("Y:");
                                gridUi.Label(EngineStates.Game.PlayerCamera.Position.Y.ToString("0.00"));
                                gridUi.EndRow();

                                gridUi.Label("Z:");
                                gridUi.Label(EngineStates.Game.PlayerCamera.Position.Z.ToString("0.00"));
                                gridUi.EndRow();

                                gridUi.Label("Chunk:");
                                ChunkCoord currentChunk =
                                    EngineStates.Game.PlayerCamera.GetChunkCoord(EngineStates.Game.PlayerCamera.Position);
                                gridUi.Label(currentChunk.ToString());
                                gridUi.EndRow();

                                gridUi.Label("FPS:");
                                gridUi.Label(EngineStates.Game.Fps.ToString("0.00"));
                                gridUi.EndRow();
                            });
                    });
                });
        }
    }
}