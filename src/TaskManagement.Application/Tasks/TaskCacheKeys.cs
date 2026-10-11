namespace TaskManagement.Application.Tasks;

public static class TaskCacheKeys
{
    public static string ForTask(Guid userId, Guid taskId) => $"task:{userId}:{taskId}";
}
