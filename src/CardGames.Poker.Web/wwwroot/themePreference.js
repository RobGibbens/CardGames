const themePreferenceKey = "themePreference";
const legacyThemeKey = "theme";
const systemThemeQuery = "(prefers-color-scheme: dark)";
const siteThemePreferenceKey = "siteThemeStylesheet";
const defaultSiteThemeStylesheet = "astrovista.css";
const availableSiteThemeStylesheets = ["astrovista.css", "claudeplus.css", "lightgreen.css"];

let mediaQueryList = null;
let systemThemeHandler = null;
let initialized = false;
let siteThemeInitialized = false;

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

export function getStoredSiteThemeStylesheet() {
    return normalizeSiteThemeStylesheet(localStorage.getItem(siteThemePreferenceKey));
}

export function applySiteThemeStylesheet(stylesheet) {
    const normalizedStylesheet = normalizeSiteThemeStylesheet(stylesheet);
    localStorage.setItem(siteThemePreferenceKey, normalizedStylesheet);
    document.documentElement.setAttribute("data-site-theme-stylesheet", normalizedStylesheet);

    const stylesheetLink = document.getElementById("site-theme-stylesheet");
    if (stylesheetLink !== null) {
        stylesheetLink.href = `css/${normalizedStylesheet}`;
    }
}

export function initializeSiteThemeStylesheet() {
    if (siteThemeInitialized) {
        return;
    }

    siteThemeInitialized = true;
    applySiteThemeStylesheet(getStoredSiteThemeStylesheet());
    document.addEventListener("change", handleSiteThemeSelectChange);
}

export function bindSiteThemeSelect(selector) {
    const themeSelect = document.querySelector(selector);
    if (!(themeSelect instanceof HTMLSelectElement)) {
        return;
    }

    themeSelect.dataset.siteThemeSelect = "";
    applySiteThemeStylesheet(themeSelect.value);
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

function normalizeSiteThemeStylesheet(value) {
    return availableSiteThemeStylesheets.includes(value)
        ? value
        : defaultSiteThemeStylesheet;
}

function handleSiteThemeSelectChange(event) {
    if (event.target instanceof HTMLSelectElement && event.target.matches("[data-site-theme-select]")) {
        applySiteThemeStylesheet(event.target.value);
        persistSiteTheme(event.target);
    }
}

async function persistSiteTheme(themeSelect) {
    const form = themeSelect.form;
    if (form === null) {
        return;
    }

    const antiforgeryToken = form.querySelector('input[name="__RequestVerificationToken"]');
    const formData = new FormData();
    formData.set("theme", themeSelect.value);

    if (antiforgeryToken instanceof HTMLInputElement) {
        formData.set(antiforgeryToken.name, antiforgeryToken.value);
    }

    const response = await fetch("/Account/Manage/SiteTheme", {
        method: "POST",
        body: formData,
        credentials: "same-origin"
    });

    if (!response.ok) {
        throw new Error(`Unable to persist site theme. Status: ${response.status}`);
    }
}

initializeThemePreference();
initializeSiteThemeStylesheet();

// Re-apply theme after Blazor enhanced navigation, which patches the DOM and
// may strip the data-theme attribute from <html> to match the server response.
// Module scripts only execute once, so initializeThemePreference won't re-run.
document.addEventListener("blazor:enhancedload", () => {
    applyThemePreference(getStoredThemePreference());
    applySiteThemeStylesheet(getStoredSiteThemeStylesheet());
});