using FluentAssertions;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;

namespace TaskManagement.UnitTests.Domain;

public class TaskItemTests
{
    [Fact]
    public void Constructor_TrimsTitle_AndNormalizesIt()
    {
        var task = new TaskItem("  Write the report  ", null, TaskPriority.Medium, Guid.NewGuid());

        task.Title.Should().Be("Write the report");
        task.NormalizedTitle.Should().Be("WRITE THE REPORT");
    }

    [Fact]
    public void Constructor_DefaultsToPending_AndSetsCreatedDate()
    {
        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());

        task.Status.Should().Be(TaskItemStatus.Pending);
        task.CreatedDate.Should().Be(DateOnly.FromDateTime(task.CreatedAt));
    }

    [Fact]
    public void ChangeStatus_UpdatesStatus()
    {
        var task = new TaskItem("Task", null, TaskPriority.Low, Guid.NewGuid());

        task.ChangeStatus(TaskItemStatus.InProgress);

        task.Status.Should().Be(TaskItemStatus.InProgress);
    }
}
