using Basics.Game;
using Basics.Game.TerrainManaging;
using Basics.Utilities;
using Basics.Window;
using Egui;

namespace Basics.Graphics.UI
{
    
    public class UIManager
    {
        private ChunkRequestor _chunkRequestor;

        public UIManager(ChunkRequestor requestor)
        {
            _chunkRequestor = requestor;
        }

        // Diese Methode wird aus dem Integration.Run() aufgerufen
        public void Draw(Context ctx)
        {
            // Globale Settings. style und ctx sind bitches die man nicht direkt bearbeiten kann
            var style = ctx.Style;
            var visuals = style.Visuals;
            visuals.WindowShadow = Shadow.None;
            visuals.PopupShadow = Shadow.None;
            style.Visuals = visuals;
            ctx.SetStyle(style);
            
            //Normal Ohne alles
            new Egui.Containers.Area("crosshair")
                .Anchor(Align2.CenterCenter, new EVec2(0, 0))
                .Show(ctx, ui => { ui.Label("+"); });
        
            
            // Window haben ctx default settings aber mit schatten overwritten
            new Egui.Containers.Window("Engine Settings")
                .Resizable((false, false))
                .Show(ctx, ui =>
                {
                    int dist = GameSettings.RenderDistance;
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref dist, 1, 40).Text("Render Distance")).Changed)
                    {
                        GameSettings.SetRenderDistance(dist);
                        //TODO: Force ChunkUpdate in ChunkRequestor without passing reference to UIManager
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
                    float speed = GameSettings.PlayerMoveSpeed;
                    if (ui.Add(new Egui.Widgets.Slider<float>(ref speed, 1f, 100f).Text("Player Speed")).Changed)
                    {
                        GameSettings.SetPlayerMoveSpeed(speed);
                    }
                    
                    float sensitivity = GameSettings.MouseSensitivity;
                    if (ui.Add(new Egui.Widgets.Slider<float>(ref sensitivity, 0.01f, 1f).Text("Mouse Sensitivity")).Changed)
                    {
                        GameSettings.SetMouseSensitivity(sensitivity);
                    }
                });
            
            // Hier ist mit einem Frame wo dann lokal extra Sachen gesezt werden
            new Egui.Containers.Area("PlayerInformation")
                .Anchor(Align2.RightTop, new EVec2(-10, 10))
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
                        frameUi.Heading("Player Information");

                        // Ein Grid für tabellarische Ausrichtung erstellen
                        new Egui.Grid("player_info_grid")
                            .Striped(true) // Optional: Fügt einen leichten Hintergrund für jede zweite Zeile hinzu
                            .Show(frameUi, gridUi =>
                            {
                                // Zeile 1: X-Koordinate
                                gridUi.Label("X:");
                                gridUi.Label(EngineStates.Game.PlayerCamera.Position.X.ToString("0.00"));
                                gridUi.EndRow(); // Beendet die aktuelle Zeile im Raster

                                // Zeile 2: Y-Koordinate
                                gridUi.Label("Y:");
                                gridUi.Label(EngineStates.Game.PlayerCamera.Position.Y.ToString("0.00"));
                                gridUi.EndRow();

                                // Zeile 3: Z-Koordinate
                                gridUi.Label("Z:");
                                gridUi.Label(EngineStates.Game.PlayerCamera.Position.Z.ToString("0.00"));
                                gridUi.EndRow();

                                // Zeile 4: Chunk
                                gridUi.Label("Chunk:");
                                ChunkCoord currentChunk =
                                    EngineStates.Game.PlayerCamera.GetChunkCoord(EngineStates.Game.PlayerCamera.Position);
                                gridUi.Label(currentChunk.ToString());
                                gridUi.EndRow();

                                // Zeile 5: FPS
                                gridUi.Label("FPS:");
                                gridUi.Label(EngineStates.Game.Fps.ToString("0.00"));
                                gridUi.EndRow();
                            });
                    });
                });
        }
    }
}