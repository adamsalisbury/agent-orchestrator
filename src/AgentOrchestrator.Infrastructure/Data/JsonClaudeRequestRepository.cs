using System.Text.Json;
using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;

namespace AgentOrchestrator.Infrastructure.Data;

public class JsonClaudeRequestRepository : IClaudeRequestRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonClaudeRequestRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "requests.json");

        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, "[]");
    }

    public async Task<ClaudeRequest> GetByIdAsync(Guid id)
    {
        var requests = await ReadAllAsync();
        return requests.FirstOrDefault(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Request {id} not found.");
    }

    public async Task<IReadOnlyList<ClaudeRequest>> GetAllAsync()
    {
        return await ReadAllAsync();
    }

    public async Task AddAsync(ClaudeRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var requests = await ReadAllAsync();
            requests.Add(request);
            await WriteAllAsync(requests);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(ClaudeRequest request)
    {
        await _lock.WaitAsync();
        try
        {
            var requests = await ReadAllAsync();
            var index = requests.FindIndex(r => r.Id == request.Id);
            if (index == -1)
                throw new KeyNotFoundException($"Request {request.Id} not found.");

            requests[index] = request;
            await WriteAllAsync(requests);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ClaudeRequest>> ReadAllAsync()
    {
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<ClaudeRequest>>(json, JsonOptions) ?? new List<ClaudeRequest>();
    }

    private async Task WriteAllAsync(List<ClaudeRequest> requests)
    {
        var json = JsonSerializer.Serialize(requests, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
