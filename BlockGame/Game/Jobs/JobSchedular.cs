using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;

namespace Basics.Game.Utilities;
using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// Verwaltet die Ausführung von Jobs über einen Thread-Pool mit Prioritäten.
/// Nutzt ein SemaphoreSlim für effizientes Thread-Waiting ohne CPU-Last.
/// </summary>
public class JobScheduler
{
    /// <summary>Queue für hochpriorisierte Jobs.</summary>
    private ConcurrentQueue<IJob> _highPriority = new ConcurrentQueue<IJob>();
    /// <summary>Queue für niedrigpriorisierte Jobs.</summary>
    private ConcurrentQueue<IJob> _lowPriority = new ConcurrentQueue<IJob>();
    
    /// <summary>Semaphore für die Steuerung der Worker-Thread-Aktivität.</summary>
    /// <remarks>Startwert ist 0 (keine Jobs da). Releasen weckt einen Schlafenden Thread auf.</remarks>
    private SemaphoreSlim _jobAvailableSignal = new SemaphoreSlim(0);
    
    /// <summary>Der für die Job-Ausführung verwendete Kontext.</summary>
    private JobContext _jobContext;
    
    /// <summary>Gibt an, ob der Scheduler aktiv ist.</summary>
    private volatile bool _isRunning = true;

    /// <summary>Liste der Worker-Threads, damit Stop() auf deren Beendigung warten kann.</summary>
    private readonly List<Thread> _workers = new();

    /// <summary>
    /// Startet den JobScheduler mit der angegebenen Anzahl von Worker-Threads.
    /// </summary>
    /// <param name="workerCount">Die Anzahl der Worker-Threads (Standard: Anzahl der CPU-Kerne).</param>
    /// <param name="jobcontext">Der für die Job-Ausführung zu verwendende <see cref="JobContext"/>.</param>
    public void Start(int workerCount, JobContext jobcontext)
    {
        _jobContext = jobcontext;
        for (int i = 0; i < workerCount; i++)
        {
            Thread worker = new Thread(WorkerLoop);
            worker.IsBackground = true; 
            worker.Start();
            _workers.Add(worker);
        }
    }

    /// <summary>
    /// Fügt einen hochpriorisierten Job der Warteschlange hinzu.
    /// </summary>
    /// <param name="job">Der hinzuzufügende <see cref="IJob"/>.</param>
    public void EnqueueHigh(IJob job)
    {
        _highPriority.Enqueue(job);
        
        // Wir signalisieren dem OS, dass EIN neuer Job da ist.
        // Das weckt sofort exakt EINEN schlafenden Worker-Thread auf!
        _jobAvailableSignal.Release(); 
    }
    
    /// <summary>
    /// Fügt einen niedrigpriorisierten Job der Warteschlange hinzu.
    /// </summary>
    /// <param name="job">Der hinzuzufügende <see cref="IJob"/>.</param>
    public void EnqueueLow(IJob job)
    {
        _lowPriority.Enqueue(job);
        _jobAvailableSignal.Release(); // Auch hier einen Worker wecken
    }

    /// <summary>
    /// Hauptschleife des Worker-Threads. Wartet auf Jobs und führt diese aus.
    /// </summary>
    /// <remarks>
    /// Der Thread verbraucht 0.0% CPU-Leistung, solange keine Jobs vorhanden sind.
    /// Beendet sich, wenn <see cref="_isRunning"/> auf <c>false</c> gesetzt wird.
    /// </remarks>
    private void WorkerLoop()
    {
        while (_isRunning)
        {
            // 1. Der Thread versucht, ein Signal zu bekommen.
            // Wenn der Zähler 0 ist, wird der Thread hier vom OS blockiert (eingefroren).
            // Er verbraucht exakt 0.0% CPU-Leistung, bis Release() gerufen wird!
            _jobAvailableSignal.Wait(); 

            // 2. Sobald er hier ankommt, WISSEN wir sicher, dass ein Job in einer der Queues liegt.
            if (_highPriority.TryDequeue(out IJob highJob))
            {
                try
                {
                    highJob.Execute(_jobContext);
                }
                catch (Exception e)
                {
                    //TODO: logging + diagnostics module compatability
                    Console.Error.WriteLine($"[JobScheduler] Job {highJob.GetType().Name} failed: {e.Message}");
                }
                
            }
            else if (_lowPriority.TryDequeue(out IJob lowJob))
            {
                try
                {
                    lowJob.Execute(_jobContext);
                }
                catch (Exception e)
                {
                    //TODO: logging + diagnostics module compatability
                    Console.Error.WriteLine($"[JobScheduler] Job {lowJob.GetType().Name} failed: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Stoppt den JobScheduler und wartet auf die Beendigung aller Worker-Threads.
    /// </summary>
    /// <remarks>
    /// Setzt <see cref="_isRunning"/> auf <c>false</c>, weckt alle schlafenden Threads
    /// und wartet mit Timeout darauf, dass sie sich beenden. Verhindert Race Conditions
    /// beim Dispose von ChunkProvider und GL-Kontext.
    /// </remarks>
    public void Stop()
    {
        _isRunning = false;
        
        // Alle schlafenden Threads wecken, damit sie das _isRunning-Flag sehen
        _jobAvailableSignal.Release(100);

        // Auf alle Worker warten (max. 2 Sekunden pro Thread)
        foreach (Thread worker in _workers)
        {
            worker.Join(TimeSpan.FromSeconds(2));
        }

        _workers.Clear();
    }
}