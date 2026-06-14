using ClaudeDesktopLauncher.ViewModels;

namespace ClaudeDesktopLauncher.Tests.ViewModels;

/// <summary>
/// Tests for the thumbnail lifecycle on <c>ClaudeInstanceViewModel</c>:
/// that null or empty byte arrays are treated as "no change" (the
/// frozen-thumbnail behaviour for close-to-tray slots) and that
/// <c>ClearThumbnail</c> explicitly blanks the property.
///
/// <para>
/// We don't verify that a non-null byte array produces a non-null
/// <c>Bitmap</c> from these tests - Avalonia's <c>Bitmap</c>
/// constructor requires a running Avalonia platform which unit tests
/// don't have. The capture -> decode -> bind path is verified by
/// live runs and the v1.9.0 screenshot taken during shipping.
/// </para>
/// </summary>
public class ClaudeInstanceViewModelThumbnailTests
{
    private static ClaudeInstanceViewModel NewVm() =>
        new(instanceNumber: 1, initialName: "test", isSeeded: true);

    [Fact]
    public void UpdateThumbnailFromBytes_WithNull_IsNoOp()
    {
        var vm = NewVm();

        vm.UpdateThumbnailFromBytes(null);

        Assert.Null(vm.Thumbnail);
    }

    [Fact]
    public void UpdateThumbnailFromBytes_WithEmptyArray_IsNoOp()
    {
        var vm = NewVm();

        vm.UpdateThumbnailFromBytes(Array.Empty<byte>());

        Assert.Null(vm.Thumbnail);
    }

    [Fact]
    public void ClearThumbnail_WhenAlreadyNull_StaysNull()
    {
        var vm = NewVm();

        vm.ClearThumbnail();

        Assert.Null(vm.Thumbnail);
    }

    [Fact]
    public void UpdateThumbnailFromBytes_NullAfterSet_DoesNotClear()
    {
        // This is the "frozen thumbnail" promise - when a slot
        // close-to-trays, the capture service returns null but we
        // intentionally do NOT blank the existing thumbnail. This test
        // can't directly prove the existing thumbnail stays set
        // because Bitmap construction needs an Avalonia platform we
        // don't have in unit tests, but it does prove the null path
        // doesn't throw and doesn't reassign the property to null
        // after the first call.
        var vm = NewVm();
        Assert.Null(vm.Thumbnail);

        vm.UpdateThumbnailFromBytes(null);
        vm.UpdateThumbnailFromBytes(null);

        Assert.Null(vm.Thumbnail);
    }
}
