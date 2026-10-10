using Labeling.Domain.Consensus;

namespace Labeling.UnitTests.Domain;

public class TextConsensusTests
{
    private static TeacherVote Vote(double weight, string? text) => new(Guid.NewGuid(), weight, [], text);

    [Theory]
    [InlineData("а123вс 77", "A123BC77")]   // строчная кириллица
    [InlineData("A 123-BC|77", "A123BC77")] // разделители
    [InlineData("  ", "")]
    public void Text_is_normalized(string raw, string expected) => Assert.Equal(expected, TextConsensus.Normalize(raw));

    [Fact]
    public void Cyrillic_and_latin_answers_count_as_one_vote()
    {
        var result = TextConsensus.Compute([Vote(1, "A123BC77"), Vote(1, "А123ВС77"), Vote(1, "A123BC71")]);

        Assert.Equal("A123BC77", result.Text);
        Assert.Equal(0.6667, result.Agreement);
    }

    [Fact]
    public void Unreadable_is_a_vote_too()
    {
        var result = TextConsensus.Compute([Vote(1, ""), Vote(1, null), Vote(1, "X1")]);

        Assert.Null(result.Text);
        Assert.Equal(0.6667, result.Agreement);
    }

    [Fact]
    public void On_a_tie_text_beats_unreadable()
    {
        var result = TextConsensus.Compute([Vote(1, "AB12"), Vote(1, "")]);
        Assert.Equal("AB12", result.Text);
        Assert.Equal(0.5, result.Agreement);
    }

    [Fact]
    public void Weights_matter()
    {
        var result = TextConsensus.Compute([Vote(3, "B777OP"), Vote(1, "B771OP"), Vote(1, "B771OP")]);
        Assert.Equal("B777OP", result.Text);
        Assert.Equal(0.6, result.Agreement);
    }
}
