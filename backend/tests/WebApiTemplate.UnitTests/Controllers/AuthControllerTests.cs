using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ApplicationCore.DTO;
using ApplicationCore.Entities;
using ApplicationCore.Exceptions;
using ApplicationCore.Interfaces;
using WebApiTemplate.TestFixtures.Builders;
using WebApiTemplate.UnitTests.Base;
using WebApi.Controllers.AuthController;

namespace WebApiTemplate.UnitTests.Controllers;

public class AuthControllerTests : TestBase
{
    private readonly Mock<IAuthService> _mockService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockService = new Mock<IAuthService>();
        _mockMapper = new Mock<IMapper>();
        _controller = new AuthController(_mockService.Object, _mockMapper.Object);

        // Setup HttpContext for controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var userDto = new UserInfoDTO
        {
            UserName = "testuser",
            Password = "Password123!"
        };

        var userEntity = new UserInfoBuilder()
            .WithUserName("testuser")
            .WithPassword("Password123!")
            .Build();

        var expectedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";

        _mockMapper.Setup(m => m.Map<UserInfo>(userDto)).Returns(userEntity);
        _mockService.Setup(s => s.Login("testuser", "Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        // Act
        var result = await _controller.Login(userDto, GetCancellationToken());

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(expectedToken);

        _mockService.Verify(s => s.Login("testuser", "Password123!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ThrowsException()
    {
        // Arrange
        var userDto = new UserInfoDTO
        {
            UserName = "testuser",
            Password = "WrongPassword"
        };

        var userEntity = new UserInfoBuilder()
            .WithUserName("testuser")
            .WithPassword("WrongPassword")
            .Build();

        _mockMapper.Setup(m => m.Map<UserInfo>(userDto)).Returns(userEntity);
        _mockService.Setup(s => s.Login("testuser", "WrongPassword", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("Invalid username or password"));

        // Act
        var act = async () => await _controller.Login(userDto, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ThrowsNotFoundException()
    {
        // Arrange
        var userDto = new UserInfoDTO
        {
            UserName = "nonexistent",
            Password = "Password123!"
        };

        var userEntity = new UserInfoBuilder()
            .WithUserName("nonexistent")
            .WithPassword("Password123!")
            .Build();

        _mockMapper.Setup(m => m.Map<UserInfo>(userDto)).Returns(userEntity);
        _mockService.Setup(s => s.Login("nonexistent", "Password123!", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("UserInfo", "nonexistent"));

        // Act
        var act = async () => await _controller.Login(userDto, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WithValidUser_ReturnsOkWithUserDto()
    {
        // Arrange
        var userDto = new UserInfoDTO
        {
            UserName = "newuser",
            Email = "newuser@example.com",
            Password = "Password123!"
        };

        var userEntity = new UserInfoBuilder()
            .WithUserName("newuser")
            .WithEmail("newuser@example.com")
            .WithPassword("Password123!")
            .Build();

        var registeredUser = new UserInfoBuilder()
            .WithId(1)
            .WithUserName("newuser")
            .WithEmail("newuser@example.com")
            .WithHashedPassword("Password123!")
            .Build();

        var registeredDto = new UserInfoDTO
        {
            Id = 1,
            UserName = "newuser",
            Email = "newuser@example.com"
            // No password in DTO response
        };

        _mockMapper.Setup(m => m.Map<UserInfo>(userDto)).Returns(userEntity);
        _mockService.Setup(s => s.Register(userEntity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registeredUser);
        _mockMapper.Setup(m => m.Map<UserInfoDTO>(registeredUser)).Returns(registeredDto);

        // Act
        var result = await _controller.Register(userDto, GetCancellationToken());

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(registeredDto);

        _mockService.Verify(s => s.Register(It.IsAny<UserInfo>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_WithExistingUsername_ThrowsConflictException()
    {
        // Arrange
        var userDto = new UserInfoDTO
        {
            UserName = "existinguser",
            Password = "Password123!"
        };

        var userEntity = new UserInfoBuilder()
            .WithUserName("existinguser")
            .WithPassword("Password123!")
            .Build();

        _mockMapper.Setup(m => m.Map<UserInfo>(userDto)).Returns(userEntity);
        _mockService.Setup(s => s.Register(userEntity, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Username 'existinguser' is already taken"));

        // Act
        var act = async () => await _controller.Register(userDto, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Register_WithInvalidData_ThrowsValidationException()
    {
        // Arrange
        var userDto = new UserInfoDTO
        {
            UserName = "",
            Password = "Password123!"
        };

        var userEntity = new UserInfoBuilder()
            .WithUserName("")
            .WithPassword("Password123!")
            .Build();

        _mockMapper.Setup(m => m.Map<UserInfo>(userDto)).Returns(userEntity);
        _mockService.Setup(s => s.Register(userEntity, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("UserName", "Username is required"));

        // Act
        var act = async () => await _controller.Register(userDto, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    #endregion
}
