namespace ClaudeDesktopLauncher.Services.Interfaces;

/// <summary>
/// Abstracts a modal Yes/No confirmation dialog so view-models can ask the
/// user to confirm destructive or unusual actions without taking a direct
/// dependency on Avalonia <see cref="Avalonia.Controls.Window"/> types.
///
/// Implementations MUST:
/// <list type="bullet">
///   <item>Return <c>false</c> if the application has no active owner
///   window (e.g. called during shutdown) - never throw in that case.</item>
///   <item>Marshal dialog creation onto the UI thread regardless of the
///   calling thread so tick-timer-driven view models can call this
///   safely.</item>
///   <item>Respect keyboard conventions: Enter confirms, Esc cancels,
///   Tab cycles between the two buttons.</item>
/// </list>
/// </summary>
public interface IConfirmDialogService
{
    Task<bool> ConfirmAsync(ConfirmDialogOptions options);
}

/// <summary>
/// Content and behaviour for a single confirmation prompt.
/// <para>
/// <paramref name="ConfirmText"/> and <paramref name="CancelText"/> default
/// to generic verbs; prefer action-specific language
/// (<c>"Close Claude"</c>, <c>"Delete slot"</c>) at the call site so the
/// button reads as the action it performs, not the word "Confirm".
/// </para>
/// </summary>
public record ConfirmDialogOptions(
    string Title,
    string Message,
    string ConfirmText = "Confirm",
    string CancelText = "Cancel",
    DialogSeverity Severity = DialogSeverity.Warning);

/// <summary>
/// Visual severity tier. Drives the accent-bar colour on the dialog and,
/// for <see cref="Destructive"/>, also tints the confirm button so the
/// user gets a final visual cue before clicking through.
/// </summary>
public enum DialogSeverity
{
    /// <summary>Neutral informational prompt. Blue accent.</summary>
    Info,

    /// <summary>Caution prompt for non-destructive but non-trivial
    /// actions. Yellow accent (matches the launcher's title-bar).</summary>
    Warning,

    /// <summary>Destructive or irreversible action. Red accent AND red
    /// confirm button text so the user gets a second-chance visual
    /// warning even if they skim-read the message.</summary>
    Destructive
}