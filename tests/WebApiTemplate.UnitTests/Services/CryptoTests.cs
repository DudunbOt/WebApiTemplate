using FluentAssertions;
using Infrastructure.Services;
using WebApiTemplate.UnitTests.Base;

namespace WebApiTemplate.UnitTests.Services;

public class CryptoTests : TestBase
{
    [Fact]
    public void HashPassword_WithValidPassword_ReturnsNonEmptyHash()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash = Crypto.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
    }

    [Fact]
    public void HashPassword_WithSamePassword_ReturnsDifferentHashes()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash1 = Crypto.HashPassword(password);
        var hash2 = Crypto.HashPassword(password);

        // Assert
        hash1.Should().NotBe(hash2, "because each hash should use a unique salt");
    }

    [Theory]
    [InlineData("Password123!")]
    [InlineData("short")]
    [InlineData("VeryLongPasswordWithLotsOfCharacters123456789!@#$%^&*()")]
    [InlineData("")]
    [InlineData(" ")]
    public void HashPassword_WithVariousPasswords_ReturnsHash(string password)
    {
        // Act
        var hash = Crypto.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VerifyHashedPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = Crypto.HashPassword(password);

        // Act
        var result = Crypto.VerifyHashedPassword(hash, password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyHashedPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var correctPassword = "MySecurePassword123!";
        var incorrectPassword = "WrongPassword456!";
        var hash = Crypto.HashPassword(correctPassword);

        // Act
        var result = Crypto.VerifyHashedPassword(hash, incorrectPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("Password123!", "password123!")]
    [InlineData("password", "Password")]
    [InlineData("TEST", "test")]
    public void VerifyHashedPassword_IsCaseSensitive(string originalPassword, string attemptedPassword)
    {
        // Arrange
        var hash = Crypto.HashPassword(originalPassword);

        // Act
        var result = Crypto.VerifyHashedPassword(hash, attemptedPassword);

        // Assert
        result.Should().BeFalse("because password verification should be case-sensitive");
    }

    [Fact]
    public void VerifyHashedPassword_WithEmptyPassword_ReturnsFalseOrHandlesGracefully()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = Crypto.HashPassword(password);

        // Act
        var result = Crypto.VerifyHashedPassword(hash, "");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyHashedPassword_WithNullPassword_ReturnsFalseOrThrows()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = Crypto.HashPassword(password);

        // Act & Assert
        var act = () => Crypto.VerifyHashedPassword(hash, null!);

        // Either returns false or throws - both are acceptable
        try
        {
            var result = act();
            result.Should().BeFalse();
        }
        catch
        {
            // Throwing is acceptable behavior for null input
            act.Should().Throw<Exception>();
        }
    }

    [Fact]
    public void VerifyHashedPassword_WithInvalidHash_ReturnsFalse()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var invalidHash = "InvalidHashValue";

        // Act
        var result = Crypto.VerifyHashedPassword(invalidHash, password);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_ProducesConsistentLength()
    {
        // Arrange
        var password1 = "short";
        var password2 = "VeryLongPasswordWithLotsOfCharacters";

        // Act
        var hash1 = Crypto.HashPassword(password1);
        var hash2 = Crypto.HashPassword(password2);

        // Assert
        hash1.Length.Should().Be(hash2.Length, "because hash length should be consistent regardless of input length");
    }

    [Fact]
    public void HashPassword_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var password = "P@ssw0rd!#$%^&*()_+{}[]|\\:;\"'<>,.?/~`";

        // Act
        var hash = Crypto.HashPassword(password);
        var verified = Crypto.VerifyHashedPassword(hash, password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        verified.Should().BeTrue();
    }

    [Fact]
    public void HashPassword_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var password = "Пароль123!密码🔒";

        // Act
        var hash = Crypto.HashPassword(password);
        var verified = Crypto.VerifyHashedPassword(hash, password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        verified.Should().BeTrue();
    }

    [Fact]
    public void VerifyHashedPassword_WithSlightlyDifferentPassword_ReturnsFalse()
    {
        // Arrange
        var password = "Password123!";
        var hash = Crypto.HashPassword(password);
        var differentPassword = "Password123";

        // Act
        var result = Crypto.VerifyHashedPassword(hash, differentPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_MultipleHashesOfSamePassword_EachVerifiesCorrectly()
    {
        // Arrange
        var password = "MySecurePassword123!";

        // Act
        var hash1 = Crypto.HashPassword(password);
        var hash2 = Crypto.HashPassword(password);
        var hash3 = Crypto.HashPassword(password);

        // Assert
        Crypto.VerifyHashedPassword(hash1, password).Should().BeTrue();
        Crypto.VerifyHashedPassword(hash2, password).Should().BeTrue();
        Crypto.VerifyHashedPassword(hash3, password).Should().BeTrue();

        hash1.Should().NotBe(hash2);
        hash2.Should().NotBe(hash3);
        hash1.Should().NotBe(hash3);
    }

    [Fact]
    public void HashPassword_IsTimingAttackResistant()
    {
        // Arrange
        var password = "MySecurePassword123!";
        var hash = Crypto.HashPassword(password);
        var iterations = 100;

        // Act - Measure time for correct password
        var correctTimes = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Crypto.VerifyHashedPassword(hash, password);
            sw.Stop();
            correctTimes.Add(sw.ElapsedTicks);
        }

        // Measure time for incorrect password
        var incorrectTimes = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Crypto.VerifyHashedPassword(hash, "WrongPassword");
            sw.Stop();
            incorrectTimes.Add(sw.ElapsedTicks);
        }

        // Assert - Times should be similar (within reasonable variance)
        var avgCorrect = correctTimes.Average();
        var avgIncorrect = incorrectTimes.Average();
        var variance = Math.Abs(avgCorrect - avgIncorrect) / avgCorrect;

        // Timing variance should be less than 50% (generous threshold for timing-attack resistance)
        variance.Should().BeLessThan(0.5, "because verification should take similar time regardless of password correctness");
    }
}
