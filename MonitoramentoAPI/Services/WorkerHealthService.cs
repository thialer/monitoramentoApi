namespace ApiMonitoramentoAPI.Services;

/// <summary>
/// Service para rastrear o status do MonitoringWorker
/// Usado para health checks e diagnóstico em produção
/// </summary>
public interface IWorkerHealthService
{
    WorkerHealthStatus GetStatus();
    void RecordCycle(int monitorsProcessed, int monitorsSkipped);
    void RecordError(Exception ex);
}

public class WorkerHealthStatus
{
    public bool IsRunning { get; set; }
    public DateTime? LastCycleTime { get; set; }
    public int TotalCyclesCompleted { get; set; }
    public int TotalErrorsEncountered { get; set; }
    public DateTime StartedAt { get; set; }
    public int MonitorsProcessedLastCycle { get; set; }
    public int MonitorsSkippedLastCycle { get; set; }
    public string? LastErrorMessage { get; set; }
}

public class WorkerHealthService : IWorkerHealthService
{
    private readonly WorkerHealthStatus _status;
    private readonly object _lockObject = new();

    public WorkerHealthService()
    {
        _status = new WorkerHealthStatus
        {
            IsRunning = true,
            StartedAt = DateTime.UtcNow,
            TotalCyclesCompleted = 0,
            TotalErrorsEncountered = 0
        };
    }

    public WorkerHealthStatus GetStatus()
    {
        lock (_lockObject)
        {
            return new WorkerHealthStatus
            {
                IsRunning = _status.IsRunning,
                LastCycleTime = _status.LastCycleTime,
                TotalCyclesCompleted = _status.TotalCyclesCompleted,
                TotalErrorsEncountered = _status.TotalErrorsEncountered,
                StartedAt = _status.StartedAt,
                MonitorsProcessedLastCycle = _status.MonitorsProcessedLastCycle,
                MonitorsSkippedLastCycle = _status.MonitorsSkippedLastCycle,
                LastErrorMessage = _status.LastErrorMessage
            };
        }
    }

    public void RecordCycle(int monitorsProcessed, int monitorsSkipped)
    {
        lock (_lockObject)
        {
            _status.LastCycleTime = DateTime.UtcNow;
            _status.TotalCyclesCompleted++;
            _status.MonitorsProcessedLastCycle = monitorsProcessed;
            _status.MonitorsSkippedLastCycle = monitorsSkipped;
        }
    }

    public void RecordError(Exception ex)
    {
        lock (_lockObject)
        {
            _status.TotalErrorsEncountered++;
            _status.LastErrorMessage = ex.Message;
        }
    }
}
