using Microsoft.EntityFrameworkCore;
using Moq;
using Infrastructure.Configurations;

namespace WebApiTemplate.UnitTests.Base;

/// <summary>
/// Base class for service layer tests providing mock DbContext and common service dependencies
/// </summary>
public abstract class ServiceTestBase : TestBase
{
    protected Mock<AppDbContext> MockContext { get; private set; }

    protected ServiceTestBase()
    {
        MockContext = CreateMockDbContext();
    }

    /// <summary>
    /// Creates a mock DbContext for testing
    /// </summary>
    protected Mock<AppDbContext> CreateMockDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockContext = new Mock<AppDbContext>(options);
        return mockContext;
    }

    /// <summary>
    /// Creates an in-memory DbContext for integration-style unit tests
    /// </summary>
    protected AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>
    /// Creates a mock DbSet for testing with provided data
    /// </summary>
    protected Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();

        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

        return mockSet;
    }

    public override void Dispose()
    {
        MockContext?.Object?.Dispose();
        base.Dispose();
    }
}
