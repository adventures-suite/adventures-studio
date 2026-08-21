export function focusElement(element) {
    element.focus();
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
