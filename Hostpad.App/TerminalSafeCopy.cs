using System.Windows;

namespace Hostpad.App;

/// <summary>
/// Strips the trailing line break from text copied out of a multi-line box.
/// <para>
/// Selecting a line by dragging to its end takes the line break with it, which
/// every text editor does and nobody notices — until the paste target is a
/// terminal, where a trailing newline is Enter. Copying a note that reads
/// <c>/etc/init.d/liferay stop</c> and pasting it into PuTTY then runs the
/// command before the user has read it back. Notes hold exactly that kind of
/// line, so the break is removed on the way to the clipboard.
/// </para>
/// <para>
/// Only what is copied changes. The selection, the caret and the text in the
/// box are untouched, so a selection that visually covers the line break still
/// deletes it when the user presses Delete.
/// </para>
/// </summary>
public static class TerminalSafeCopy
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(TerminalSafeCopy),
            new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(DependencyObject element, bool value) =>
        element.SetValue(EnabledProperty, value);

    public static bool GetEnabled(DependencyObject element) =>
        (bool)element.GetValue(EnabledProperty);

    /// <summary>
    /// Removes line breaks from the end of <paramref name="text"/>, leaving the
    /// rest alone. Returns null when there is nothing left to copy, which the
    /// caller treats as "do not interfere": replacing a copied blank line with
    /// an empty clipboard would look like the copy had failed.
    /// </summary>
    internal static string? TrimTrailingLineBreaks(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var end = text.Length;
        while (end > 0 && (text[end - 1] == '\n' || text[end - 1] == '\r'))
        {
            end--;
        }

        if (end == text.Length)
        {
            return null;
        }

        return end == 0 ? null : text[..end];
    }

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement target)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            DataObject.AddCopyingHandler(target, OnCopying);
        }
        else
        {
            DataObject.RemoveCopyingHandler(target, OnCopying);
        }
    }

    /// <summary>
    /// Runs for Ctrl+C, the context menu and cut alike, and for dragging text
    /// out of the box, which is why it is hooked here rather than on the copy
    /// command: one place covers every route to the clipboard.
    /// </summary>
    private static void OnCopying(object sender, DataObjectCopyingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string copied)
        {
            return;
        }

        if (TrimTrailingLineBreaks(copied) is not { } trimmed)
        {
            return;
        }

        // The event's data object cannot be swapped for another one, so the
        // text formats are overwritten in place. Both are set because pasting
        // applications pick whichever they prefer.
        e.DataObject.SetData(DataFormats.UnicodeText, trimmed);
        e.DataObject.SetData(DataFormats.Text, trimmed);
    }
}
