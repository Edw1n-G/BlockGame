using System.Diagnostics;

namespace Basics.Game.Utilities;

/// <summary>Runs CPU jobs on session-owned workers. A scheduler is single-use.</summary>
public class JobScheduler
{
    private enum State { Created, Running, Stopping, Stopped }
    private readonly object _gate = new();
    private readonly Queue<IJob> _high = new();
    private readonly Queue<IJob> _low = new();
    private readonly List<Thread> _workers = new();
    private State _state;
    private JobContext? _context;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Exception> _failures = new();

    /// <summary>Failures are retained for diagnostics rather than lost on worker threads.</summary>
    public IReadOnlyCollection<Exception> Failures => _failures.ToArray();

    public void Start(int workerCount, JobContext jobcontext)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workerCount);
        ArgumentNullException.ThrowIfNull(jobcontext);
        lock (_gate)
        {
            if (_state != State.Created) throw new InvalidOperationException("Scheduler has already started or stopped.");
            _context = jobcontext;
            _state = State.Running;
            for (int i = 0; i < workerCount; i++)
            {
                var worker = new Thread(WorkerLoop) { IsBackground = true, Name = $"Chunk worker {i}" };
                _workers.Add(worker);
                worker.Start();
            }
            Monitor.PulseAll(_gate);
        }
    }

    public bool EnqueueHigh(IJob job) => Enqueue(job, _high);
    public bool EnqueueLow(IJob job) => Enqueue(job, _low);

    private bool Enqueue(IJob job, Queue<IJob> queue)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_gate)
        {
            if (_state is State.Stopping or State.Stopped) return false;
            queue.Enqueue(job);
            Monitor.Pulse(_gate);
            return true;
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            IJob job;
            lock (_gate)
            {
                while (_state == State.Running && _high.Count == 0 && _low.Count == 0) Monitor.Wait(_gate);
                if (_state != State.Running) return;
                job = _high.Count > 0 ? _high.Dequeue() : _low.Dequeue();
            }
            try { job.Execute(_context!); }
            catch (Exception ex)
            {
                _failures.Enqueue(ex);
                Console.Error.WriteLine($"[JobScheduler] {job.GetType().Name} failed: {ex}");
            }
        }
    }

    /// <summary>Discards waiting work, then waits up to one deadline for in-flight jobs.</summary>
    public bool Stop(TimeSpan? timeout = null)
    {
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(10);
        if (limit < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var clock = Stopwatch.StartNew();
        lock (_gate)
        {
            if (_state == State.Stopped) return true;
            if (_state != State.Stopping)
            {
                _state = State.Stopping;
                // Cancellation is bookkeeping only; no GPU operations are allowed here.
                foreach (IJob job in _high.Concat(_low))
                    try { job.Discard(_context); }
                    catch (Exception ex) { _failures.Enqueue(ex); Console.Error.WriteLine($"[JobScheduler] Discard failed: {ex}"); }
                _high.Clear();
                _low.Clear();
                Monitor.PulseAll(_gate);
            }
        }
        foreach (Thread worker in _workers)
        {
            TimeSpan remaining = limit - clock.Elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            if (worker == Thread.CurrentThread || !worker.Join(remaining))
            {
                Console.Error.WriteLine("[JobScheduler] Timed out waiting for workers; resources remain alive.");
                return false;
            }
        }
        lock (_gate) _state = State.Stopped;
        return true;
    }
}
