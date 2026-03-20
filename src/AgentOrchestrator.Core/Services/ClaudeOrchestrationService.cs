using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Core.Services;

public class ClaudeOrchestrationService
{
    private readonly IClaudeRequestRepository _repository;
    private readonly IClaudeCodeRunner _runner;

    public ClaudeOrchestrationService(IClaudeRequestRepository repository, IClaudeCodeRunner runner)
    {
        _repository = repository;
        _runner = runner;
    }

    public async Task<ClaudeRequest> SubmitRequestAsync(string prompt)
    {
        var request = new ClaudeRequest { Prompt = prompt };
        await _repository.AddAsync(request);

        _ = Task.Run(async () => await ProcessRequestAsync(request.Id));

        return request;
    }

    public async Task<ClaudeRequest> GetRequestAsync(Guid id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<ClaudeRequest>> GetAllRequestsAsync()
    {
        return await _repository.GetAllAsync();
    }

    private async Task ProcessRequestAsync(Guid requestId)
    {
        var request = await _repository.GetByIdAsync(requestId);
        request.Status = ClaudeRequestStatus.Processing;
        await _repository.UpdateAsync(request);

        try
        {
            var response = await _runner.ExecuteAsync(request.Prompt);
            request.Response = response;
            request.Status = ClaudeRequestStatus.Completed;
            request.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            request.Status = ClaudeRequestStatus.Failed;
            request.ErrorMessage = ex.Message;
            request.CompletedAt = DateTime.UtcNow;
        }

        await _repository.UpdateAsync(request);
    }
}
