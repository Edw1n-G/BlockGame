namespace Basics.Game.Utilities;

/// <summary>
/// Interface für alle ausführbaren Jobs im JobScheduler.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Führt den Job mit den bereitgestellten Kontext aus.
    /// </summary>
    /// <param name="context">Der <see cref="JobContext"/> mit den notwendigen Ressourcen für den Job.</param>
    void Execute(JobContext context);
}