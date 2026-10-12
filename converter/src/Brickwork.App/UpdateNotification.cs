using Avalonia.Controls;
using Avalonia.Threading;
using Brickwork.App.Views.Dialogs;

namespace Brickwork.App;

/// <summary>Runs the startup update check without blocking the main window.</summary>
public static class UpdateNotification
{
    public static void ScheduleStartupCheck(Window owner)
    {
        _ = RunStartupCheckAsync(owner);
    }

    private static async Task RunStartupCheckAsync(Window owner)
    {
        try
        {
            var service = new UpdateCheckService();
            var result = await service.CheckAsync(respectSkipped: true).ConfigureAwait(true);

            if (!result.UpdateAvailable || result.AlreadySkipped || result.CheckFailed)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (owner.IsVisible)
                {
                    UpdateAvailableWindow.Show(owner, result, service);
                }
            });
        }
        catch
        {
            // Startup checks must never affect app launch.
        }
    }
}
