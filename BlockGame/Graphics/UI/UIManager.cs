using Basics.Common;
using Basics.Engine;
using Basics.Settings;
using Basics.Terrain.Selection;
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
        private readonly System.Diagnostics.Stopwatch _throughputClock = System.Diagnostics.Stopwatch.StartNew();
        private Basics.Diagnostics.PipelineCounts _previousCounts;
        private double _lastThroughput;
        private string _throughputText = "Chunk throughput: collecting...";
        
        /// <summary>
        /// Ereignis, das ausgelöst wird, wenn sich die Render-Distance geändert hat.
        /// </summary>
        public static event Action OnRenderDistanceChanged;

        public static void NotifyRenderDistanceChanged() => OnRenderDistanceChanged?.Invoke();

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
            ctx.StyleMut((ref Style style) =>
            {
                var visuals = style.Visuals;
                visuals.WindowShadow = Shadow.None;
                visuals.PopupShadow = Shadow.None;
                style.Visuals = visuals;
            });
            
            new Egui.Containers.Area("crosshair")
                .Anchor(Align2.CenterCenter, new EVec2(0, 0))
                .Show(ctx, ui => { ui.Label("+"); });
        
            var provider = EngineContext.Current?.ChunkProvider;
            double now = _throughputClock.Elapsed.TotalSeconds;
            if (provider != null && now - _lastThroughput >= 1)
            {
                var counts = provider.Metrics.Snapshot();
                var delta = counts - _previousCounts;
                double seconds = now - _lastThroughput;
                var queues = provider.JobScheduler!.QueueDepths;
                _throughputText = $"Chunks/s  gen {delta.Generated / seconds:F0}  mesh {delta.Meshed / seconds:F0}  upload {delta.Uploaded / seconds:F0}\nQueues  gen {queues.Generation}  mesh {queues.Mesh + provider.PendingMeshRequests.Count}  upload {provider.UploadQueue.Count}";
                _previousCounts = counts; _lastThroughput = now;
            }
            new Egui.Containers.Area("ChunkThroughput")
                .Anchor(Align2.LeftBottom, new EVec2(10, -10))
                .Show(ctx, ui => new Egui.Containers.Frame { Fill = Color32.Black }
                    .Show(ui, content => { content.SetWidth(350); content.Label(_throughputText); }));

            if (Basics.Diagnostics.FrameBenchmarkRun.Options == null)
            {
                new Egui.Containers.Window("Engine Settings")
                    .Resizable((false, false))
                    .Show(ctx, ui =>
                    {
                        int hdist = _settings.RenderDistance;
                        int vdist = _settings.VerticalRenderDistance;
                        if (ui.Add(new Egui.Widgets.Slider<int>(ref hdist, 1, 60).Text("Horizontal Render Distance")).Changed)
                        {
                            _settings.SetRenderDistance(hdist);
                            NotifyRenderDistanceChanged();
                        }
                        if (ui.Add(new Egui.Widgets.Slider<int>(ref vdist, 1, 30).Text("Vertical Render Distance")).Changed)
                        {
                            _settings.SetVerticalRenderDistance(vdist);
                            NotifyRenderDistanceChanged();
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
            
            }

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
                                gridUi.Label(global::Basics.States.Game.PlayerCamera.Position.X.ToString("0.00"));
                                gridUi.EndRow();

                                gridUi.Label("Y:");
                                gridUi.Label(global::Basics.States.Game.PlayerCamera.Position.Y.ToString("0.00"));
                                gridUi.EndRow();

                                gridUi.Label("Z:");
                                gridUi.Label(global::Basics.States.Game.PlayerCamera.Position.Z.ToString("0.00"));
                                gridUi.EndRow();

                                gridUi.Label("Chunk:");
                                ChunkCoord currentChunk =
                                    global::Basics.States.Game.PlayerCamera.GetChunkCoord(global::Basics.States.Game.PlayerCamera.Position);
                                gridUi.Label(currentChunk.ToString());
                                gridUi.EndRow();

                                gridUi.Label("FPS (1s):");
                                gridUi.Label(global::Basics.States.Game.Fps.ToString("0.00"));
                                gridUi.EndRow();
                                gridUi.Label("Frame ms (1s):");
                                gridUi.Label(global::Basics.States.Game.FrameMilliseconds.ToString("0.00"));
                                gridUi.EndRow();
                            });
                    });
                });
        }
    }
}
