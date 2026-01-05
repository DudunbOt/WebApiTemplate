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

        // Setup JWT settings
        _mockJwtSettings.Setup(x => x.Value).Returns(new JwtSettings
        {
            Key = "TEST_SECRET_KEY_FOR_JWT_TOKEN_GENERATION_12345",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryInMinutes = 30
        });

        // Setup AppConfig
        _mockAppConfig.Setup(x => x.Value).Returns(new AppConfig
        {
            UseCache = false // Disable caching for unit tests
        });

        // Create in-memory database context
        _context = CreateInMemoryDbContext();

        _service = new UserInfoService(_context, _mockCache.Object, _mockJwtSettings.Object, _mockAppConfig.Object);
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidUser_CreatesUserAndHashesPassword()
    {
        // Arrange
        var Users = new UserInfoBuilder()
            .WithUserName("newuser")
            .WithEmail("newuser@example.com")
            .WithPassword("PlainPassword123!")
            .Build();
        Users.Id = 0; // New user

        // Act
        var result = await _service.Register(Users, GetCancellationToken());

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.UserName.Should().Be("newuser");
        result.Email.Should().Be("newuser@example.com");
        result.Password.Should().NotBe("PlainPassword123!", "because password should be hashed");
        result.Password.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithNullUserInfo_ThrowsArgumentNullException()
    {
        // Arrange
        UserInfo? userInfo = null;

        // Act
        var act = async () => await _service.Register(userInfo!, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("userInfo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_WithInvalidUserName_ThrowsValidationException(string? username)
    {
        // Arrange
        var userInfo = new UserInfoBuilder()
            .WithUserName(username!)
            .WithPassword("Password123!")
            .Build();
        userInfo.Id = 0;

        // Act
        var act = async () => await _service.Register(userInfo, GetCancellationToken());

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("UserName");
        exception.Which.Errors["UserName"].Should().Contain("Username is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_WithInvalidPassword_ThrowsValidationException(string? password)
    {
        // Arrange
        var userInfo = new UserInfoBuilder()
            .WithUserName("validuser")
            .WithPassword(password!)
            .Build();
        userInfo.Id = 0;

        // Act
        var act = async () => await _service.Register(userInfo, GetCancellationToken());

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Password");
        exception.Which.Errors["Password"].Should().Contain("Password is required");
    }

    [Fact]
    public async Task Register_WithExistingUserName_ThrowsConflictException()
    {
        // Arrange - Create existing user
        var existingUser = new UserInfoBuilder()
            .WithUserName("existinguser")
            .WithHashedPassword("Password123!")
            .Build();
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        // Try to register with same username
        var newUser = new UserInfoBuilder()
            .WithUserName("existinguser")
            .WithPassword("DifferentPassword!")
            .Build();
        newUser.Id = 0;

        // Act
        var act = async () => await _service.Register(newUser, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*existinguser*already taken*");
    }

    [Fact]
    public async Task Register_HashedPasswordCanBeVerified()
    {
        // Arrange
        var plainPassword = "MySecurePassword123!";
        var Users = new UserInfoBuilder()
            .WithUserName("testuser")
            .WithPassword(plainPassword)
            .Build();
        Users.Id = 0;

        // Act
        var result = await _service.Register(Users, GetCancellationToken());

        // Assert
        var verified = Crypto.VerifyHashedPassword(result?.Password, plainPassword);
        verified.Should().BeTrue("because the stored password should verify against the original plain password");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtToken()
    {
        // Arrange
        var plainPassword = "Password123!";
        var user = UserInfoBuilder.WithKnownPassword(plainPassword);
        user.UserName = "loginuser";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var token = await _service.Login("loginuser", plainPassword, GetCancellationToken());

        // Assert
        token.Should().NotBeNullOrEmpty();

        // Verify it's a valid JWT token
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();

        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public async Task Login_JwtTokenContainsCorrectClaims()
    {
        // Arrange
        var plainPassword = "Password123!";
        var user = UserInfoBuilder.WithKnownPassword(plainPassword);
        user.UserName = "claimuser";
        user.Id = 42;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var token = await _service.Login("claimuser", plainPassword, GetCancellationToken());

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name);
        nameClaim.Should().NotBeNull();
        nameClaim!.Value.Should().Be("claimuser");

        var idClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        idClaim.Should().NotBeNull();
        idClaim!.Value.Should().Be("42");
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ThrowsNotFoundException()
    {
        // Arrange - No user in database
        var username = "nonexistentuser";
        var password = "Password123!";

        // Act
        var act = async () => await _service.Login(username, password, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*nonexistentuser*");
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var correctPassword = "CorrectPassword123!";
        var user = UserInfoBuilder.WithKnownPassword(correctPassword);
        user.UserName = "testuser";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var incorrectPassword = "WrongPassword456!";

        // Act
        var act = async () => await _service.Login("testuser", incorrectPassword, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid username or password*");
    }

    [Fact]
    public async Task Login_WithDeletedUser_ThrowsNotFoundException()
    {
        // Arrange
        var password = "Password123!";
        var user = UserInfoBuilder.WithKnownPassword(password);
        user.UserName = "deleteduser";
        user.DeletedDate = DateTime.UtcNow;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _service.Login("deleteduser", password, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>("because deleted users should not be able to login");
    }

    [Fact]
    public async Task Login_IsCaseSensitiveForPassword()
    {
        // Arrange
        var password = "Password123!";
        var user = UserInfoBuilder.WithKnownPassword(password);
        user.UserName = "casesensitiveuser";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _service.Login("casesensitiveuser", "password123!", GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Login_TokenExpirationIsSet()
    {
        // Arrange
        var password = "Password123!";
        var user = UserInfoBuilder.WithKnownPassword(password);
        user.UserName = "expiryuser";
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var token = await _service.Login("expiryuser", password, GetCancellationToken());

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
        jwtToken.ValidTo.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(31));
    }

    #endregion

    #region ResetPassword Tests

    [Fact]
    public async Task ResetPassword_ThrowsNotImplementedException()
    {
        // Arrange
        var user = UserInfoBuilder.Default();

        // Act
        var act = async () => await _service.ResetPassword(user, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    #endregion

    public override void Dispose()
    {
        _context?.Dispose();
        base.Dispose();
    }
}
