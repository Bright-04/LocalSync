using System.Security.Cryptography;
using LocalSync.Core.Security;

namespace LocalSync.Core.Tests.Security;

public class Base32Tests
{
    [Fact]
    public void Encode_ProducesRfc4648Alphabet()
    {
        var encoded = Base32.Encode(RandomNumberGenerator.GetBytes(32));

        Assert.Equal(52, encoded.Length);
        Assert.All(encoded, c => Assert.Contains(c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
    }

    [Fact]
    public void RoundTrips_AcrossManyRandomInputs()
    {
        for (var i = 0; i < 500; i++)
        {
            var original = RandomNumberGenerator.GetBytes(32);

            Assert.True(Base32.TryDecode(Base32.Encode(original), out var decoded));
            Assert.Equal(original, decoded);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void RoundTrips_AtVariousLengths(int length)
    {
        var original = RandomNumberGenerator.GetBytes(length);

        Assert.True(Base32.TryDecode(Base32.Encode(original), out var decoded));
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_IsDeterministic()
    {
        var data = new byte[32];
        data[0] = 0xAB;
        data[31] = 0xCD;

        Assert.Equal(Base32.Encode(data), Base32.Encode(data));
    }

    [Fact]
    public void Encode_OfEmpty_IsEmpty() => Assert.Equal(string.Empty, Base32.Encode([]));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("!!!!")]
    public void TryDecode_RejectsGarbage(string? input) =>
        Assert.False(Base32.TryDecode(input, out _));

    [Theory]
    // The glyphs humans substitute, and the separators they insert.
    [InlineData("0", "O")]
    [InlineData("1", "I")]
    [InlineData("8", "B")]
    [InlineData("abc", "ABC")]
    [InlineData("AB-CD", "ABCD")]
    [InlineData("AB CD", "ABCD")]
    [InlineData("AB_CD", "ABCD")]
    public void Normalize_MapsConfusableInput(string input, string expected) =>
        Assert.Equal(expected, Base32.Normalize(input));

    [Fact]
    public void CheckCharacter_DetectsEverySingleCharacterSubstitution()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var payload = Base32.Encode(RandomNumberGenerator.GetBytes(8));
        var correct = Base32.ComputeCheckCharacter(payload);

        var undetected = 0;
        for (var position = 0; position < payload.Length; position++)
        {
            foreach (var replacement in alphabet)
            {
                if (replacement == payload[position])
                {
                    continue;
                }

                var typo = payload.ToCharArray();
                typo[position] = replacement;

                if (Base32.ComputeCheckCharacter(new string(typo)) == correct)
                {
                    undetected++;
                }
            }
        }

        Assert.Equal(0, undetected);
    }

    [Fact]
    public void CheckCharacter_RejectsNonAlphabetInput() =>
        Assert.Throws<ArgumentException>(() => Base32.ComputeCheckCharacter("!"));
}
