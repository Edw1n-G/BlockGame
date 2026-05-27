using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Generation;

namespace Basics.Game.Utilities;
using System.Collections.Concurrent;
using System.Threading;

public class JobScheduler
{
    private ConcurrentQueue<IJob> _highPriority = new ConcurrentQueue<IJob>();
    private ConcurrentQueue<IJob> _lowPriority = new ConcurrentQueue<IJob>();
    
    // NEU: Das Semaphore. Startwert ist 0 (keine Jobs da).
    private SemaphoreSlim _jobAvailableSignal = new SemaphoreSlim(0);
    
    private JobContext _jobContext;
    
    private bool _isRunning = true;

    public void Start(int workerCount, JobContext jobcontext)
    {
        _jobContext = jobcontext;
        for (int i = 0; i < workerCount; i++)
        {
            Thread worker = new Thread(WorkerLoop);
            worker.IsBackground = true; 
            worker.Start();
        }
    }

    public void EnqueueHigh(IJob job)
    {
        _highPriority.Enqueue(job);
        
        // NEU: Wir signalisieren dem OS, dass EIN neuer Job da ist.
        // Das weckt sofort exakt EINEN schlafenden Worker-Thread auf!
        _jobAvailableSignal.Release(); 
    }
    
    public void EnqueueLow(IJob job)
    {
        _lowPriority.Enqueue(job);
        _jobAvailableSignal.Release(); // Auch hier einen Worker wecken
    }

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
                highJob.Execute(_jobContext);
            }
            else if (_lowPriority.TryDequeue(out IJob lowJob))
            {
                lowJob.Execute(_jobContext);
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
        
        // Wenn wir die Engine beenden, müssen wir alle schlafenden Threads
        // einmal "wecken", damit sie merken, dass _isRunning false ist und sie sich beenden können.
        _jobAvailableSignal.Release(100); 
    }
}