using Xunit;

namespace Hostpad.App.Tests;

public sealed class TerminalSafeCopyTests
{
    [Theory]
    [InlineData("sudo systemctl restart nginx\n", "sudo systemctl restart nginx")]
    [InlineData("sudo systemctl restart nginx\r\n", "sudo systemctl restart nginx")]
    [InlineData("ls -la\r\n\r\n\n", "ls -la")]
    [InlineData("first\nsecond\n", "first\nsecond")]
    public void TrimsLineBreaksFromTheEnd(string copied, string expected) =>
        Assert.Equal(expected, TerminalSafeCopy.TrimTrailingLineBreaks(copied));

    [Fact]
    public void LeavesLineBreaksInsideTheTextAlone() =>
        Assert.Equal("one\r\ntwo", TerminalSafeCopy.TrimTrailingLineBreaks("one\r\ntwo\r\n"));

    [Fact]
    public void KeepsTrailingSpaces()
    {
        // Only Enter is dangerous in a terminal; spaces are left as copied.
        Assert.Equal("echo hi  ", TerminalSafeCopy.TrimTrailingLineBreaks("echo hi  \n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nothing to trim")]
    public void ReturnsNullWhenThereIsNothingToChange(string? copied) =>
        Assert.Null(TerminalSafeCopy.TrimTrailingLineBreaks(copied));

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n\r\n")]
    public void ReturnsNullForABlankLineRatherThanAnEmptyClipboard(string copied) =>
        Assert.Null(TerminalSafeCopy.TrimTrailingLineBreaks(copied));
}
