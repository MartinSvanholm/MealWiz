namespace TheMealWorkshop.Shared.Services.ScreenWakeLock;

/// <summary>
/// Keeps the screen on while the cooking view is open. The only code in Shared that calls the
/// <c>js/screenWakeLock.js</c> module. Where the browser has no screen wake lock, or refuses it, nothing happens and
/// the page works the same.
/// </summary>
public interface IScreenWakeLock
{
    /// <summary>Keeps the screen on until <see cref="ReleaseAsync"/>. False where unsupported or refused. Never throws.</summary>
    ValueTask<bool> RequestAsync();

    /// <summary>Lets the screen sleep again. Safe when nothing is held. Never throws.</summary>
    ValueTask ReleaseAsync();
}
