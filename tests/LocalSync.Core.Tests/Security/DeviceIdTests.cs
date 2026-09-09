using System.Security.Cryptography;
using LocalSync.Core.Security;

namespace LocalSync.Core.Tests.Security;

public class DeviceIdTests
{
    [Fact]
    public void FromBytes_RequiresExactly32Bytes()
    {
        Assert.Throws<ArgumentException>(() => DeviceId.FromBytes(new byte[31]));
        Assert.Throws<ArgumentException>(() => DeviceId.FromBytes(new byte[33]));

        _ = DeviceId.FromBytes(new byte[32]);
    }

    [Fact]
    public void RoundTripsThroughItsCanonicalForm()
    {
        var original = DeviceId.CreateRandom();

        Assert.True(DeviceId.TryParse(original.ToString(), out var parsed));
        Assert.Equal(original, parsed);
        Assert.Equal(original.ToByteArray(), parsed.ToByteArray());
    }

    [Fact]
    public void ParsesCaseInsensitivelyAndIgnoresSeparators()
    {
        var original = DeviceId.CreateRandom();
        var canonical = original.ToString();

        Assert.True(DeviceId.TryParse(canonical.ToLowerInvariant(), out var lower));
        Assert.Equal(original, lower);

        var spaced = string.Join(' ', Enumerable.Range(0, 4).Select(i => canonical.Substring(i * 13, 13)));
        Assert.True(DeviceId.TryParse(spaced, out var withSpaces));
        Assert.Equal(original, withSpaces);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOOSHORT")]
    public void TryParse_RejectsMalformedInput(string? input) =>
        Assert.False(DeviceId.TryParse(input, out _));

    [Fact]
    public void TryParse_RejectsWrongLength()
    {
        var tooLong = DeviceId.CreateRandom().ToString() + "AAAA";

        Assert.False(DeviceId.TryParse(tooLong, out _));
    }

    [Fact]
    public void FromSubjectPublicKeyInfo_IsSha256OfTheInput()
    {
        var spki = RandomNumberGenerator.GetBytes(91);

        var id = DeviceId.FromSubjectPublicKeyInfo(spki);

        Assert.Equal(SHA256.HashData(spki), id.ToByteArray());
    }

    [Fact]
    public void FromSubjectPublicKeyInfo_IsStableForTheSameKey()
    {
        var spki = RandomNumberGenerator.GetBytes(91);

        Assert.Equal(DeviceId.FromSubjectPublicKeyInfo(spki), DeviceId.FromSubjectPublicKeyInfo(spki));
    }

    [Fact]
    public void DistinctKeysProduceDistinctIds()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => DeviceId.CreateRandom()).ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void DisplayString_IsFourCheckedGroups()
    {
        var display = DeviceId.CreateRandom().ToDisplayString();
        var groups = display.Split('-');

        Assert.Equal(4, groups.Length);
        Assert.All(groups, g => Assert.Equal(14, g.Length));
    }

    [Fact]
    public void DisplayString_CheckCharacterCatchesASingleMistypedCharacter()
    {
        var display = DeviceId.CreateRandom().ToDisplayString();
        var groups = display.Split('-');
        var group = groups[0];

        var payload = group[..13];
        var check = group[13];
        var mistyped = (payload[3] == 'A' ? 'B' : 'A');
        var typoPayload = payload[..3] + mistyped + payload[4..];

        Assert.NotEqual(check, Base32.ComputeCheckCharacter(typoPayload));
    }

    [Fact]
    public void Mnemonic_IsFiveLowercaseWords()
    {
        var words = DeviceId.CreateRandom().ToMnemonic().Split('-');

        Assert.Equal(5, words.Length);
        Assert.All(words, w =>
        {
            Assert.NotEmpty(w);
            Assert.True(w.All(char.IsLower), $"'{w}' is not lowercase");
        });
    }

    [Fact]
    public void Mnemonic_IsStableForTheSameId()
    {
        var id = DeviceId.CreateRandom();

        Assert.Equal(id.ToMnemonic(), id.ToMnemonic());
    }

    [Fact]
    public void Mnemonic_DiffersBetweenIds()
    {
        var mnemonics = Enumerable.Range(0, 200)
            .Select(_ => DeviceId.CreateRandom().ToMnemonic())
            .ToHashSet();

        // 40 bits of entropy: collisions in 200 draws would indicate a bug,
        // not bad luck.
        Assert.Equal(200, mnemonics.Count);
    }

    [Fact]
    public void Default_IsEmptyAndRendersEmpty()
    {
        DeviceId none = default;

        Assert.True(none.IsEmpty);
        Assert.Equal(string.Empty, none.ToString());
        Assert.Equal(string.Empty, none.ToDisplayString());
        Assert.Equal(string.Empty, none.ToMnemonic());
    }

    [Fact]
    public void EqualityIsByValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var a = DeviceId.FromBytes(bytes);
        var b = DeviceId.FromBytes(bytes);
        var c = DeviceId.CreateRandom();

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a != c);
        Assert.False(a.Equals("not a device id"));
    }
}
