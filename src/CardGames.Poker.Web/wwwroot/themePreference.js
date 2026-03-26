const themePreferenceKey = "themePreference";
const legacyThemeKey = "theme";
const systemThemeQuery = "(prefers-color-scheme: dark)";

let mediaQueryList = null;
let systemThemeHandler = null;
let initialized = false;

export function getStoredThemePreference() {
    return normalizePreference(localStorage.getItem(themePreferenceKey) ?? localStorage.getItem(legacyThemeKey));
}

export function applyThemePreference(preference) {
    const normalizedPreference = normalizePreference(preference);

    if (normalizedPreference === "system") {
        attachSystemThemeListener();

        const effectiveTheme = getSystemPrefersDark() ? "dark" : "light";
        persistThemePreference(normalizedPreference, effectiveTheme);
        applyTheme(effectiveTheme);
        return;
    }

    detachSystemThemeListener();
    persistThemePreference(normalizedPreference, normalizedPreference);
    applyTheme(normalizedPreference);
}

export function initializeThemePreference() {
    if (initialized) {
        return;
    }

    initialized = true;
    applyThemePreference(getStoredThemePreference());
}

function getSystemPrefersDark() {
    return window.matchMedia(systemThemeQuery).matches;
}

function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
}

function persistThemePreference(preference, effectiveTheme) {
    localStorage.setItem(themePreferenceKey, preference);
    localStorage.setItem(legacyThemeKey, effectiveTheme);
}

function attachSystemThemeListener() {
    if (systemThemeHandler !== null) {
        return;
    }

    mediaQueryList = window.matchMedia(systemThemeQuery);
    systemThemeHandler = (event) => {
        const effectiveTheme = event.matches ? "dark" : "light";
        persistThemePreference("system", effectiveTheme);
        applyTheme(effectiveTheme);
    };

    if (typeof mediaQueryList.addEventListener === "function") {
        mediaQueryList.addEventListener("change", systemThemeHandler);
    } else {
        mediaQueryList.addListener(systemThemeHandler);
    }
}

function detachSystemThemeListener() {
    if (mediaQueryList === null || systemThemeHandler === null) {
        return;
    }

    if (typeof mediaQueryList.removeEventListener === "function") {
        mediaQueryList.removeEventListener("change", systemThemeHandler);
    } else {
        mediaQueryList.removeListener(systemThemeHandler);
    }

    mediaQueryList = null;
    systemThemeHandler = null;
}

function normalizePreference(value) {
    return value === "light" || value === "dark" || value === "system"
        ? value
        : "system";
}

initializeThemePreference();