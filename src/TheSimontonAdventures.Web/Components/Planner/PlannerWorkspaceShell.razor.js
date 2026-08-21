export function focusElement(element) {
    element.focus();
}

const workspacePreferencesKey = 'adventures-suite.workspace.preferences.v1';

export function readWorkspacePreferences() {
    try {
        const parsed = JSON.parse(window.localStorage.getItem(workspacePreferencesKey));
        if (!parsed || typeof parsed !== 'object') {
            return null;
        }

        return {
            theme: ['light', 'dark', 'system'].includes(parsed.theme) ? parsed.theme : 'system',
            isSidebarCollapsed: parsed.isSidebarCollapsed === true,
            isSidebarHidden: parsed.isSidebarHidden === true,
            sidebarWidthPixels: Number.isInteger(parsed.sidebarWidthPixels)
                ? parsed.sidebarWidthPixels
                : 280
        };
    } catch {
        return null;
    }
}

export function writeWorkspacePreferences(preferences) {
    try {
        window.localStorage.setItem(workspacePreferencesKey, JSON.stringify(preferences));
    } catch {
        // Preferences are optional; workspace navigation remains usable without storage.
    }
}

const pinnedHeaderObservers = new WeakMap();

export function observePinnedHeader(shell, header) {
    if (!shell || !header || pinnedHeaderObservers.has(shell)) {
        return;
    }

    const updateOffset = () => {
        shell.style.setProperty(
            '--planner-pinned-header-height',
            `${header.getBoundingClientRect().height}px`);
    };
    const observer = new ResizeObserver(updateOffset);
    observer.observe(header);
    pinnedHeaderObservers.set(shell, observer);
    updateOffset();
}

export function disconnectPinnedHeader(shell) {
    const observer = shell ? pinnedHeaderObservers.get(shell) : null;
    observer?.disconnect();
    if (shell) {
        shell.style.removeProperty('--planner-pinned-header-height');
        pinnedHeaderObservers.delete(shell);
    }
}
