namespace HardwareStore.Core.Services;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface IRateLimitService
{
    Task<bool> CheckSearchLimitAsync(User user);
    Task IncrementSearchCountAsync(User user);
}

public class RateLimitService : IRateLimitService
{
    private readonly IUserRepository _userRepository;

    public RateLimitService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> CheckSearchLimitAsync(User user)
    {
        if (user.SearchLimitResetDate.Date < DateTime.UtcNow.Date)
        {
            user.SearchesUsedToday = 0;
            user.SearchLimitResetDate = DateTime.UtcNow.Date;
            await _userRepository.UpdateAsync(user);
        }
        return user.SearchesUsedToday < user.DailySearchLimit;
    }

    public async Task IncrementSearchCountAsync(User user)
    {
        if (user.SearchLimitResetDate.Date < DateTime.UtcNow.Date)
        {
            user.SearchesUsedToday = 0;
            user.SearchLimitResetDate = DateTime.UtcNow.Date;
        }
        user.SearchesUsedToday++;
        await _userRepository.UpdateAsync(user);
    }
}
