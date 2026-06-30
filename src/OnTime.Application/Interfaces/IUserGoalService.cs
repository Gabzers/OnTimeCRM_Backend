using OnTime.Application.DTOs.Goals;

namespace OnTime.Application.Interfaces;

public interface IUserGoalService
{
    Task<IEnumerable<GoalProgressDto>> GetGoalsAsync(Guid userId, CancellationToken ct = default);
    Task<UserGoalDto> CreateGoalAsync(Guid userId, CreateUserGoalRequest request, CancellationToken ct = default);
    Task<UserGoalDto> UpdateGoalAsync(Guid userId, Guid goalId, UpdateUserGoalRequest request, CancellationToken ct = default);
    Task DeleteGoalAsync(Guid userId, Guid goalId, CancellationToken ct = default);
    Task ReorderGoalsAsync(Guid userId, ReorderGoalsRequest request, CancellationToken ct = default);
}
