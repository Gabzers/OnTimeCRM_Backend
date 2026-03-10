using OnTimeCRM.Application.DTOs.Goals;

namespace OnTimeCRM.Application.Interfaces;

public interface IUserGoalService
{
    Task<IEnumerable<GoalProgressDto>> GetGoalsAsync(Guid userId, CancellationToken ct = default);
    Task<UserGoalDto> CreateGoalAsync(Guid userId, CreateUserGoalRequest request, CancellationToken ct = default);
    Task<UserGoalDto> UpdateGoalAsync(Guid userId, Guid goalId, UpdateUserGoalRequest request, CancellationToken ct = default);
    Task DeleteGoalAsync(Guid userId, Guid goalId, CancellationToken ct = default);
}
