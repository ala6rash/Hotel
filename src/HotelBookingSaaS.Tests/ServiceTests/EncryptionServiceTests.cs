using FluentAssertions;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HotelBookingSaaS.Tests.ServiceTests;

public class EncryptionServiceTests
{
    private readonly EncryptionService _sut;

    public EncryptionServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "TestEncryptionKey_ForUnitTests_Only_32"
            })
            .Build();
        _sut = new EncryptionService(config);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalValue()
    {
        var plain = "super-secret-api-key-12345";
        var encrypted = _sut.Encrypt(plain);
        var decrypted = _sut.Decrypt(encrypted);

        decrypted.Should().Be(plain);
    }

    [Fact]
    public void Encrypt_ProducesENCPrefix()
    {
        var encrypted = _sut.Encrypt("test");
        encrypted.Should().StartWith("ENC:");
    }

    [Fact]
    public void Encrypt_SameValueTwice_ProducesDifferentCiphertext()
    {
        // Each call generates a new IV → different ciphertext
        var plain = "same-value";
        var enc1 = _sut.Encrypt(plain);
        var enc2 = _sut.Encrypt(plain);

        enc1.Should().NotBe(enc2);
        _sut.Decrypt(enc1).Should().Be(plain);
        _sut.Decrypt(enc2).Should().Be(plain);
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmpty()
    {
        _sut.Decrypt(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void IsEncrypted_ReturnsTrueForEncryptedValue()
    {
        var enc = _sut.Encrypt("value");
        _sut.IsEncrypted(enc).Should().BeTrue();
    }

    [Fact]
    public void IsEncrypted_ReturnsFalseForPlaintext()
    {
        _sut.IsEncrypted("plain-text").Should().BeFalse();
    }

    [Theory]
    [InlineData("Booking.com API Key")]
    [InlineData("Airbnb OAuth Token")]
    [InlineData("Sky Horizon Password123!@#")]
    [InlineData("عربي نص للاختبار")]
    public void Encrypt_Decrypt_VariousInputs(string input)
    {
        _sut.Decrypt(_sut.Encrypt(input)).Should().Be(input);
    }
}
