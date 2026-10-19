const listeners = new WeakMap();

export function watchOutside(root, component) {
    stopWatching(root);
    const listener = event => {
        if (!root.contains(event.target)) {
            component.invokeMethodAsync("CloseFromOutsideAsync");
        }
    };
    listeners.set(root, listener);
    document.addEventListener("pointerdown", listener, true);
}

export function stopWatching(root) {
    const listener = listeners.get(root);
    if (!listener) return;
    document.removeEventListener("pointerdown", listener, true);
    listeners.delete(root);
}

export function scrollOptionIntoView(root, optionId) {
    root.querySelector(`#${CSS.escape(optionId)}`)?.scrollIntoView({ block: "nearest" });
}
