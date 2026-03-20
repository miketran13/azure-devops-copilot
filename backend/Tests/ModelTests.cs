using DevOpsCopilot.Models;
using FluentAssertions;
using Xunit;

namespace DevOpsCopilot.Tests;

public class ModelTests
{
    [Fact]
    public void ChatRequest_RequiredMessage_IsSet()
    {
        var request = new ChatRequest
        {
            Message = "Show me active bugs",
            ProjectName = "MyProject",
            OrganizationUrl = "https://dev.azure.com/myorg"
        };

        request.Message.Should().Be("Show me active bugs");
        request.ProjectName.Should().Be("MyProject");
        request.OrganizationUrl.Should().Be("https://dev.azure.com/myorg");
        request.ConversationHistory.Should().BeNull();
    }

    [Fact]
    public void ChatRequest_WithHistory_DeserializesCorrectly()
    {
        var request = new ChatRequest
        {
            Message = "Create a bug",
            ConversationHistory =
            [
                new ConversationMessage { Role = "user", Content = "Hello" },
                new ConversationMessage { Role = "assistant", Content = "Hi! How can I help?" }
            ]
        };

        request.ConversationHistory.Should().HaveCount(2);
        request.ConversationHistory![0].Role.Should().Be("user");
    }

    [Fact]
    public void WorkItemSummary_DefaultValues()
    {
        var summary = new WorkItemSummary
        {
            Id = 42,
            Title = "Login page crashes",
            WorkItemType = "Bug",
            State = "Active"
        };

        summary.Id.Should().Be(42);
        summary.AssignedTo.Should().BeNull();
        summary.Priority.Should().BeNull();
        summary.Tags.Should().BeNull();
    }

    [Fact]
    public void ChatResponse_WithWorkItems()
    {
        var response = new ChatResponse
        {
            Reply = "Found 2 bugs.",
            WorkItems =
            [
                new WorkItemSummary { Id = 1, Title = "Bug 1", WorkItemType = "Bug", State = "Active" },
                new WorkItemSummary { Id = 2, Title = "Bug 2", WorkItemType = "Bug", State = "New" }
            ],
            SuggestedActions = ["View details", "Create a fix"]
        };

        response.Reply.Should().Contain("2 bugs");
        response.WorkItems.Should().HaveCount(2);
        response.SuggestedActions.Should().HaveCount(2);
    }
}
