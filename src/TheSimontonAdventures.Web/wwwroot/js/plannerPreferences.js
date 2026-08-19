export function readPageSize(key) {
    let value;
    try {
        value = window.localStorage.getItem(key);
    } catch {
        return null;
    }

    if (value === null) {
        return null;
    }

    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) ? parsed : null;
}

export function writePageSize(key, value) {
    try {
        window.localStorage.setItem(key, String(value));
    } catch {
        // The preference is optional; planning remains fully usable without storage.
    }
}
