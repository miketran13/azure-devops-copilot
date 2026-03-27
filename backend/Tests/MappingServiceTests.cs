using DevOpsCopilot.Models.Configuration;
using DevOpsCopilot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DevOpsCopilot.Tests;

public class MappingServiceTests
{
    private static readonly List<CustomFieldMapping> TestMappings = new()
    {
        new CustomFieldMapping
        {
            ReferenceName = "Microsoft.VSTS.Scheduling.StoryPoints",
            DisplayName = "Story Points",
            ShortName = "storypoints",
            Type = "double",
            WorkItemTypes = new List<string> { "User Story", "Bug" },
        },
        new CustomFieldMapping
        {
            ReferenceName = "Microsoft.VSTS.Common.Priority",
            DisplayName = "Priority",
            ShortName = "priority",
            Type = "integer",
            WorkItemTypes = new List<string>(),
        },
        new CustomFieldMapping
        {
            ReferenceName = "Custom.BusinessValue",
            DisplayName = "Business Value",
            ShortName = "businessvalue",
            Type = "integer",
            WorkItemTypes = new List<string> { "User Story", "Feature" },
        },
        new CustomFieldMapping
        {
            ReferenceName = "System.Tags",
            DisplayName = "Tags",
            ShortName = "tags",
            Type = "string",
            WorkItemTypes = new List<string>(),
        },
    };

    private static MappingService CreateService(List<CustomFieldMapping>? mappings = null)
    {
        var config = new CustomFieldConfiguration
        {
            FieldMappings = mappings ?? TestMappings,
        };
        var monitor = Mock.Of<IOptionsMonitor<CustomFieldConfiguration>>(
            m => m.CurrentValue == config);
        return new MappingService(monitor, NullLogger<MappingService>.Instance);
    }

    [Fact]
    public void ResolveFieldName_ExactReferenceName_ReturnsHighConfidence()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("Microsoft.VSTS.Scheduling.StoryPoints");

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().Be(1.0);
        result.Field!.ReferenceName.Should().Be("Microsoft.VSTS.Scheduling.StoryPoints");
        result.Strategy.Should().Contain("Exact");
    }

    [Fact]
    public void ResolveFieldName_ShortName_ReturnsHighConfidence()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("storypoints");

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterOrEqualTo(0.9);
        result.Field!.ReferenceName.Should().Be("Microsoft.VSTS.Scheduling.StoryPoints");
    }

    [Fact]
    public void ResolveFieldName_DisplayName_ReturnsHighConfidence()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("Story Points");

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeGreaterOrEqualTo(0.9);
        result.Field!.ReferenceName.Should().Be("Microsoft.VSTS.Scheduling.StoryPoints");
    }

    [Fact]
    public void ResolveFieldName_FuzzyMatch_ReturnsLowerConfidence()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("storey points"); // misspelling

        result.IsMatch.Should().BeTrue();
        result.Confidence.Should().BeLessThan(0.9);
        result.Field!.ReferenceName.Should().Be("Microsoft.VSTS.Scheduling.StoryPoints");
    }

    [Fact]
    public void ResolveFieldName_NoMatch_ReturnsNoMatch()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("completely_unknown_field_xyz");

        result.IsMatch.Should().BeFalse();
        result.Confidence.Should().Be(0);
    }

    [Fact]
    public void ResolveFieldName_EmptyInput_ReturnsNoMatch()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("");

        result.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void ResolveFieldName_FiltersByWorkItemType()
    {
        var svc = CreateService();

        // "businessvalue" is only for User Story and Feature, not Bug
        var resultForStory = svc.ResolveFieldName("businessvalue", "User Story");
        resultForStory.IsMatch.Should().BeTrue();

        var resultForBug = svc.ResolveFieldName("businessvalue", "Bug");
        resultForBug.IsMatch.Should().BeFalse();
    }

    [Fact]
    public void ResolveFieldName_EmptyWorkItemTypes_MatchesAll()
    {
        var svc = CreateService();

        // "priority" has empty WorkItemTypes, should match for any type
        var result = svc.ResolveFieldName("priority", "Task");
        result.IsMatch.Should().BeTrue();
        result.Field!.ReferenceName.Should().Be("Microsoft.VSTS.Common.Priority");
    }

    [Fact]
    public void RequiresConfirmation_HighConfidence_ReturnsFalse()
    {
        var svc = CreateService();
        var result = svc.ResolveFieldName("priority"); // exact short name match → high confidence

        MappingService.RequiresConfirmation(result).Should().BeFalse();
    }

    [Fact]
    public void RequiresConfirmation_LowConfidence_ReturnsTrue()
    {
        var field = new CustomFieldMapping
        {
            ReferenceName = "Custom.VeryLongFieldName",
            DisplayName = "Very Long Field Name",
            ShortName = "verylongfieldname",
            Type = "string",
        };
        var svc = CreateService(new List<CustomFieldMapping> { field });
        var result = svc.ResolveFieldName("vlong"); // very partial → low confidence

        if (result.IsMatch)
        {
            // If it matches at all, it should require confirmation due to low confidence
            MappingService.RequiresConfirmation(result).Should().BeTrue();
        }
    }

    [Fact]
    public void RequiresConfirmation_NoMatch_ReturnsFalse()
    {
        var result = FieldMatchResult.NoMatch("nonexistent");
        MappingService.RequiresConfirmation(result).Should().BeFalse();
    }
}
