using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using ApplicationCore.Entities;
using ApplicationCore.Exceptions;
using Infrastructure.Configurations;
using Infrastructure.Services;
using WebApiTemplate.TestFixtures.Builders;
using WebApiTemplate.UnitTests.Base;

namespace WebApiTemplate.UnitTests.Services;

public class UserInfoServiceTests : ServiceTestBase
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IOptions<JwtSettings>> _mockJwtSettings;
    private readonly Mock<IOptions<AppConfig>> _mockAppConfig;
    private readonly UserInfoService _service;
    private readonly AppDbContext _context;

    public UserInfoServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockJwtSettings = new Mock<IOptions<JwtSettings>>();
        _mockAppConfig = new Mock<IOptions<AppConfig>>();

        // Setup AppConfig
        _mockAppConfig.Setup(x => x.Value).Returns(new AppConfig
        {
            UseCache = false // Disable caching for unit tests
        });

        // Create in-memory database context
        _context = CreateInMemoryDbContext();

        _service = new UserInfoService(_context, _mockCache.Object, _mockAppConfig.Object, default);
    }

    public override void Dispose()
    {
        _context?.Dispose();
        base.Dispose();
    }
}
