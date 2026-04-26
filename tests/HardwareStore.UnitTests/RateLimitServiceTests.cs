using HardwareStore.Core.Services;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using Moq;

namespace HardwareStore.UnitTests;

public class RateLimitServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly RateLimitService _service;

    public RateLimitServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _service = new RateLimitService(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task CheckSearchLimitAsync_WhenUnderLimit_ReturnsTrue()
    {
        var user = new User
        {
            SearchesUsedToday = 5,
            DailySearchLimit = 10,
            SearchLimitResetDate = DateTime.UtcNow.Date
        };

        var result = await _service.CheckSearchLimitAsync(user);

        Assert.True(result);
    }

    [Fact]
    public async Task CheckSearchLimitAsync_WhenAtLimit_ReturnsFalse()
    {
        var user = new User
        {
            SearchesUsedToday = 10,
            DailySearchLimit = 10,
            SearchLimitResetDate = DateTime.UtcNow.Date
        };

        var result = await _service.CheckSearchLimitAsync(user);

        Assert.False(result);
    }

    [Fact]
    public async Task CheckSearchLimitAsync_WhenResetDatePassed_ResetsCountAndReturnsTrue()
    {
        var user = new User
        {
            SearchesUsedToday = 10,
            DailySearchLimit = 10,
            SearchLimitResetDate = DateTime.UtcNow.Date.AddDays(-1)
        };
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).ReturnsAsync(user);

        var result = await _service.CheckSearchLimitAsync(user);

        Assert.True(result);
        Assert.Equal(0, user.SearchesUsedToday);
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task IncrementSearchCountAsync_IncrementsCount()
    {
        var user = new User
        {
            SearchesUsedToday = 3,
            DailySearchLimit = 10,
            SearchLimitResetDate = DateTime.UtcNow.Date
        };
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).ReturnsAsync(user);

        await _service.IncrementSearchCountAsync(user);

        Assert.Equal(4, user.SearchesUsedToday);
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task IncrementSearchCountAsync_WhenResetDatePassed_ResetsAndIncrements()
    {
        var user = new User
        {
            SearchesUsedToday = 5,
            DailySearchLimit = 10,
            SearchLimitResetDate = DateTime.UtcNow.Date.AddDays(-1)
        };
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).ReturnsAsync(user);

        await _service.IncrementSearchCountAsync(user);

        Assert.Equal(1, user.SearchesUsedToday);
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}
