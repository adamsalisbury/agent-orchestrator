using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Models;
using AgentOrchestrator.Core.Services;
using NSubstitute;

namespace AgentOrchestrator.Core.Tests;

public class ThreadOrchestrationServiceTests
{
    private readonly IThreadRepository _threadRepo;
    private readonly IAgentRepository _agentRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IClaudeCodeRunner _runner;
    private readonly ThreadOrchestrationService _service;

    public ThreadOrchestrationServiceTests()
    {
        _threadRepo = Substitute.For<IThreadRepository>();
        _agentRepo = Substitute.For<IAgentRepository>();
        _projectRepo = Substitute.For<IProjectRepository>();
        _runner = Substitute.For<IClaudeCodeRunner>();

        _threadRepo.GetThreadMessagesAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<ThreadMessage>());

        _service = new ThreadOrchestrationService(_threadRepo, _agentRepo, _runner, _projectRepo);
    }

    [Fact]
    public async Task SendMessageAsync_CreatesUserAndAgentMessages()
    {
        _threadRepo.GetThreadMessagesAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<ThreadMessage>());

        var result = await _service.SendMessageAsync("agent1", null, "Hello");

        Assert.Equal(MessageDirection.Outbound, result.Direction);
        Assert.Equal("Hello", result.Content);
        Assert.Equal(MessageStatus.Completed, result.Status);
        Assert.Equal(1, result.MessageNumber);

        // Should have saved 2 messages: user outbound + agent pending
        await _threadRepo.Received(2).SaveMessageAsync("agent1", Arg.Any<ThreadMessage>());
    }

    [Fact]
    public async Task SendMessageAsync_UsesExistingThreadId()
    {
        _threadRepo.GetThreadMessagesAsync("agent1", "thread1")
            .Returns(new List<ThreadMessage>());

        var result = await _service.SendMessageAsync("agent1", "thread1", "Hello");

        Assert.Equal("thread1", result.ThreadId);
    }

    [Fact]
    public async Task SendMessageAsync_GeneratesNewThreadId_WhenNull()
    {
        _threadRepo.GetThreadMessagesAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<ThreadMessage>());

        var result = await _service.SendMessageAsync("agent1", null, "Hello");

        Assert.NotNull(result.ThreadId);
        Assert.NotEmpty(result.ThreadId);
    }

    [Fact]
    public async Task SendMessageAsync_ContinuesFromLastMessageNumber()
    {
        _threadRepo.GetThreadMessagesAsync("agent1", "thread1")
            .Returns(new List<ThreadMessage>
            {
                new() { ThreadId = "thread1", MessageNumber = 1 },
                new() { ThreadId = "thread1", MessageNumber = 2 }
            });

        var result = await _service.SendMessageAsync("agent1", "thread1", "Reply");

        Assert.Equal(3, result.MessageNumber);
    }

    [Fact]
    public async Task SendMessageAsync_SavesUserMessageAsCompleted()
    {
        _threadRepo.GetThreadMessagesAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<ThreadMessage>());

        await _service.SendMessageAsync("agent1", null, "Hello");

        await _threadRepo.Received(1).SaveMessageAsync("agent1",
            Arg.Is<ThreadMessage>(m =>
                m.Direction == MessageDirection.Outbound &&
                m.Status == MessageStatus.Completed &&
                m.Content == "Hello"));
    }

    [Fact]
    public async Task SendMessageAsync_SavesAgentPlaceholderAsPending()
    {
        _threadRepo.GetThreadMessagesAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new List<ThreadMessage>());

        await _service.SendMessageAsync("agent1", null, "Hello");

        await _threadRepo.Received(1).SaveMessageAsync("agent1",
            Arg.Is<ThreadMessage>(m =>
                m.Direction == MessageDirection.Inbound &&
                m.Status == MessageStatus.Pending &&
                m.Content == string.Empty));
    }

    [Fact]
    public async Task GetAllMessagesAsync_DelegatesToRepository()
    {
        var expected = new List<ThreadMessage> { new() { Content = "test" } };
        _threadRepo.GetAllMessagesAsync("agent1").Returns(expected);

        var result = await _service.GetAllMessagesAsync("agent1");

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetThreadMessagesAsync_DelegatesToRepository()
    {
        var expected = new List<ThreadMessage> { new() { Content = "test" } };
        _threadRepo.GetThreadMessagesAsync("agent1", "thread1").Returns(expected);

        var result = await _service.GetThreadMessagesAsync("agent1", "thread1");

        Assert.Same(expected, result);
    }

    [Fact]
    public void GenerateId_ReturnsFiveCharacterString()
    {
        var id = ThreadOrchestrationService.GenerateId();

        Assert.Equal(5, id.Length);
        Assert.All(id.ToCharArray(), c => Assert.True(char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void GenerateId_ProducesUniqueIds()
    {
        var ids = Enumerable.Range(0, 100)
            .Select(_ => ThreadOrchestrationService.GenerateId())
            .ToHashSet();

        // With 36^5 possibilities (~60M), 100 should all be unique
        Assert.Equal(100, ids.Count);
    }
}
