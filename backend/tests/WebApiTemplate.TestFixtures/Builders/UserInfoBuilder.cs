using ApplicationCore.Entities;
using Infrastructure.Services;

namespace WebApiTemplate.TestFixtures.Builders;

/// <summary>
/// Test data builder for UserInfo entities following the Builder pattern
/// </summary>
public class UserInfoBuilder
{
    private int _id = 1;
    private string _userName = "testuser";
    private string _email = "test@example.com";
    private string _password = "hashedPassword123";
    private DateTime _createdDate = DateTime.UtcNow;
    private DateTime? _updatedDate;
    private DateTime? _deletedDate;

    public UserInfoBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public UserInfoBuilder WithUserName(string userName)
    {
        _userName = userName;
        return this;
    }

    public UserInfoBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserInfoBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public UserInfoBuilder WithHashedPassword(string plainPassword)
    {
        _password = Crypto.HashPassword(plainPassword);
        return this;
    }

    public UserInfoBuilder WithCreatedDate(DateTime createdDate)
    {
        _createdDate = createdDate;
        return this;
    }

    public UserInfoBuilder WithUpdatedDate(DateTime? updatedDate)
    {
        _updatedDate = updatedDate;
        return this;
    }

    public UserInfoBuilder AsDeleted(DateTime? deletedDate = null)
    {
        _deletedDate = deletedDate ?? DateTime.UtcNow;
        return this;
    }

    public UserInfo Build()
    {
        return new UserInfo
        {
            Id = _id,
            UserName = _userName,
            Email = _email,
            Password = _password,
            CreatedDate = _createdDate,
            UpdatedDate = _updatedDate,
            DeletedDate = _deletedDate
        };
    }

    /// <summary>
    /// Creates a default UserInfo for testing
    /// </summary>
    public static UserInfo Default() => new UserInfoBuilder().Build();

    /// <summary>
    /// Creates multiple UserInfo entities for testing
    /// </summary>
    public static List<UserInfo> CreateMany(int count)
    {
        var users = new List<UserInfo>();
        for (int i = 1; i <= count; i++)
        {
            users.Add(new UserInfoBuilder()
                .WithId(i)
                .WithUserName($"user{i}")
                .WithEmail($"user{i}@example.com")
                .Build());
        }
        return users;
    }

    /// <summary>
    /// Creates a UserInfo with default password for login testing
    /// </summary>
    public static UserInfo WithKnownPassword(string password = "Password123!")
    {
        return new UserInfoBuilder()
            .WithHashedPassword(password)
            .Build();
    }
}
