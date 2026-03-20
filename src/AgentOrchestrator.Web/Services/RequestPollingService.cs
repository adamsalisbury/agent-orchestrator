using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentOrchestrator.Web.Services;

public class RequestPollingService : BackgroundService
{
    private readonly PendingMessageTracker _tracker;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IThreadRepository _threadRepo;
    private readonly ILogger<RequestPollingService> _logger;

    public RequestPollingService(
        PendingMessageTracker tracker,
        IHubContext<NotificationHub> hubContext,
        IThreadRepository threadRepo,
        ILogger<RequestPollingService> logger)
    {
        _tracker = tracker;
        _hubContext = hubContext;
        _threadRepo = threadRepo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(2000, stoppingToken);

            foreach (var pending in _tracker.GetAll().ToList())
            {
                try
                {
                    var msg = await _threadRepo.GetMessageAsync(pending.AgentId, pending.ThreadId, pending.MessageNumber);

                    if (msg != null && msg.Status is MessageStatus.Completed or MessageStatus.Failed)
                    {
                        await _hubContext.Clients.All.SendAsync("MessageCompleted", new
                        {
                            msg.ThreadId,
                            msg.MessageNumber,
                            msg.MessageId,
                            Direction = msg.Direction.ToString(),
                            msg.Content,
                            SentAt = msg.SentAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                            Status = msg.Status.ToString(),
                            pending.AgentId,
                            pending.AgentName
                        }, stoppingToken);

                        _tracker.Remove(pending.Key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error polling pending message {Key}", pending.Key);
                }
            }
        }
    }
}
