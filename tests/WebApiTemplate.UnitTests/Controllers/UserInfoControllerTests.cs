using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ApplicationCore.DTO;
using ApplicationCore.Entities;
using ApplicationCore.Exceptions;
using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using ApplicationCore.Entities.Base;
using ApplicationCore.Specifications;
using WebApiTemplate.TestFixtures.Builders;
using WebApiTemplate.UnitTests.Base;
using WebApi.Controllers.UserInfoController;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace WebApiTemplate.UnitTests.Controllers;

public class UserInfoControllerTests : TestBase
{
    private readonly Mock<IUserInfoService> _mockService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly UserInfoController _controller;

    public UserInfoControllerTests()
    {
        _mockService = new Mock<IUserInfoService>();
        _mockMapper = new Mock<IMapper>();
        _controller = new UserInfoController(_mockService.Object, _mockMapper.Object);

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

    #region GetUser Tests

    [Fact]
    public async Task GetUser_WithValidId_ReturnsOkWithUserDto()
    {
        // Arrange
        var userId = 1;
        var userEntity = new UserInfoBuilder()
            .WithId(userId)
            .WithUserName("testuser")
            .WithEmail("test@example.com")
            .Build();

        var userDto = new UserInfoDTO
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com"
        };

        _mockService.Setup(s => s.GetOne(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userEntity);
        _mockMapper.Setup(m => m.Map<UserInfoDTO>(userEntity)).Returns(userDto);

        // Act
        var result = await _controller.GetUser(userId, GetCancellationToken());

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(userDto);

        _mockService.Verify(s => s.GetOne(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUser_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var userId = 999;

        _mockService.Setup(s => s.GetOne(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("UserInfo", userId.ToString()));

        // Act
        var act = async () => await _controller.GetUser(userId, GetCancellationToken());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region GetUsers Tests

    [Fact]
    public async Task GetUsers_WithNoFilters_ReturnsAllUsers()
    {
        // Arrange
        var users = UserInfoBuilder.CreateMany(3);
        var userDtos = new List<UserInfoDTO>
        {
            new UserInfoDTO { Id = 1, UserName = "user1" },
            new UserInfoDTO { Id = 2, UserName = "user2" },
            new UserInfoDTO { Id = 3, UserName = "user3" }
        };

        var pagination = new Pagination
        {
            CurrentPage = 1,
            PageSize = 10,
            TotalPages = 1,
            TotalItems = 3
        };

        _mockService.Setup(s => s.GetCount(
            It.IsAny<UserInfoSpecification>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<FilterDescriptor>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagination);

        _mockService.Setup(s => s.GetList(
            It.IsAny<UserInfoSpecification>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<SortDescriptor>>(),
            It.IsAny<List<FilterDescriptor>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        _mockMapper.Setup(m => m.Map<List<UserInfoDTO>>(users)).Returns(userDtos);

        // Act
        var result = await _controller.GetUsers(GetCancellationToken());

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        // Verify the response structure
        okResult!.Value.Should().NotBeNull();
        var response = okResult.Value;

        // Use reflection to check the anonymous object properties
        var pageInfoProperty = response!.GetType().GetProperty("pageInfo");
        var usersProperty = response.GetType().GetProperty("users");

        pageInfoProperty.Should().NotBeNull();
        usersProperty.Should().NotBeNull();

        var pageInfoValue = pageInfoProperty!.GetValue(response);
        var usersValue = usersProperty!.GetValue(response) as List<UserInfoDTO>;

        pageInfoValue.Should().BeEquivalentTo(pagination);
        usersValue.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUsers_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var users = new List<UserInfo>();
        var pagination = new Pagination { CurrentPage = 1, PageSize = 10, TotalPages = 0, TotalItems = 0 };

        _mockService.Setup(s => s.GetCount(
            It.IsAny<UserInfoSpecification>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<FilterDescriptor>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagination);

        _mockService.Setup(s => s.GetList(
            It.IsAny<UserInfoSpecification>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<SortDescriptor>>(),
            It.IsAny<List<FilterDescriptor>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        _mockMapper.Setup(m => m.Map<List<UserInfoDTO>>(users)).Returns(new List<UserInfoDTO>());

        // Act
        await _controller.GetUsers(GetCancellationToken());

        // Assert
        _mockService.Verify(s => s.GetCount(
            It.IsAny<UserInfoSpecification>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<FilterDescriptor>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockService.Verify(s => s.GetList(
            It.IsAny<UserInfoSpecification>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<List<SortDescriptor>>(),
            It.IsAny<List<FilterDescriptor>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
