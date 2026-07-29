namespace WebApiTemplate.UnitTests.Base;

/// <summary>
/// Base class for all unit tests providing common test setup and utilities
/// </summary>
public abstract class TestBase : IDisposable
{
    protected TestBase()
    {
        // Common setup for all tests
    }

    public virtual void Dispose()
    {
        // Common cleanup for all tests
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a CancellationToken for testing async methods
    /// </summary>
    protected CancellationToken GetCancellationToken()
    {
        return CancellationToken.None;
    }

    /// <summary>
    /// Creates a CancellationTokenSource with timeout for testing
    /// </summary>
    protected CancellationTokenSource GetCancellationTokenSource(int milliseconds = 5000)
    {
        return new CancellationTokenSource(milliseconds);
    }
}
