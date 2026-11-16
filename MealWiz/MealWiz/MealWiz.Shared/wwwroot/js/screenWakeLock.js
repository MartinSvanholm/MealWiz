// Keeps the screen on while the cooking view is open.
// Used only through IScreenWakeLock (Services/ScreenWakeLock/ScreenWakeLock.cs).

let sentinel = null;
let wanted = false;

async function acquire() {
    try {
        sentinel = await navigator.wakeLock.request("screen");
        sentinel.addEventListener("release", () => { sentinel = null; });
        return true;
    } catch {
        sentinel = null;
        return false;
    }
}

// The browser drops the lock when the tab is hidden; take it again when the tab is back.
document.addEventListener("visibilitychange", async () => {
    if (wanted && !sentinel && document.visibilityState === "visible") {
        await acquire();
    }
});

// Returns true when the screen is kept on; false where wake lock is missing or refused.
export async function request() {
    if (!("wakeLock" in navigator)) return false;
    wanted = true;
    if (sentinel) return true;
    const ok = await acquire();
    return ok;
}

export async function release() {
    wanted = false;
    const current = sentinel;
    sentinel = null;
    if (!current) return;
    try { await current.release(); } catch { /* already released */ }
}
