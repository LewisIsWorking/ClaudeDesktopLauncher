using Avalonia.Media.Imaging;

namespace ClaudeDesktopLauncher.ViewModels;

/// <summary>
/// Shared surface for view-model rows that participate in the thumbnail
/// capture pipeline. Implemented by both <see cref="ClaudeInstanceViewModel"/>
/// (launcher-managed + tray-resident slots) and
/// <see cref="ExternalInstanceViewModel"/> (default-profile Claude
/// windows we didn't spawn). Introduced in v1.9.1 so
/// <see cref="ThumbnailRefresher"/> can treat both row types
/// uniformly without imposing a common base class.
///
/// <para>
/// The semantics contract - especially the null-is-a-no-op rule on
/// <see cref="UpdateThumbnailFromBytes"/> - is the same for every
/// implementer, because it's the rule the caller
/// (<see cref="IWindowThumbnailService"/>) depends on to deliver
/// frozen-thumbnail behaviour for windows that have gone away.
/// </para>
/// </summary>
public interface IThumbnailableViewModel
{
    /// <summary>
    /// Operating-system process id. Used by the thumbnail service to
    /// locate the target window. Stable for the lifetime of the row
    /// (rows are removed from their parent collection when the PID
    /// exits).
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Current thumbnail bitmap, or null if nothing has been captured
    /// yet. Observable so views can bind directly.
    /// </summary>
    Bitmap? Thumbnail { get; }

    /// <summary>
    /// Replaces <see cref="Thumbnail"/> with a new bitmap decoded from
    /// the supplied PNG bytes. A null or empty array is a <b>no-op</b>,
    /// not a clear - implementers must preserve the previous thumbnail
    /// so close-to-tray'd slots retain their last captured frame. The
    /// previous bitmap is disposed when a new one replaces it.
    /// </summary>
    void UpdateThumbnailFromBytes(byte[]? pngBytes);

    /// <summary>
    /// Explicitly blanks the thumbnail and disposes its unmanaged
    /// buffer. Only called when the user toggles the feature off or
    /// the row is about to be removed.
    /// </summary>
    void ClearThumbnail();
}
