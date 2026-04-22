function getSupportedTimeZones() {
    if (typeof Intl.supportedValuesOf === 'function') {
        return Intl.supportedValuesOf('timeZone');
    }

    return [Intl.DateTimeFormat().resolvedOptions().timeZone];
}

const calendarObservers = new WeakMap();

function applyCalendarHighlights(container, dateKeys) {
    if (!container) {
        return;
    }

    const lookup = new Set(dateKeys || []);
    const highlightedButtons = container.querySelectorAll('button.season-detail-calendar-day-has-event');
    for (const button of highlightedButtons) {
        button.classList.remove('season-detail-calendar-day-has-event');
    }

    const buttons = container.querySelectorAll('button[id*="-day-"]');
    for (const button of buttons) {
        const match = /-day-(\d{8})$/.exec(button.id);
        if (!match) {
            continue;
        }

        if (lookup.has(match[1])) {
            button.classList.add('season-detail-calendar-day-has-event');
        }
    }
}

export function getTimeZoneSetup() {
    const browserTimeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const uniqueTimeZones = Array.from(new Set(getSupportedTimeZones().filter(Boolean)));
    const orderedIds = [browserTimeZoneId, ...uniqueTimeZones.filter(id => id !== browserTimeZoneId)];

    return {
        browserTimeZoneId,
        timeZones: orderedIds.map(id => ({
            id,
            displayName: id.replaceAll('_', ' ')
        }))
    };
}

export function updateCalendarEventHighlights(container, dateKeys) {
    if (!container) {
        return;
    }

    let state = calendarObservers.get(container);
    if (!state) {
        const observer = new MutationObserver(() => {
            const current = calendarObservers.get(container);
            if (current) {
                applyCalendarHighlights(container, current.dateKeys);
            }
        });

        observer.observe(container, {
            childList: true,
            subtree: true
        });

        state = { observer, dateKeys: [] };
        calendarObservers.set(container, state);
    }

    state.dateKeys = [...(dateKeys || [])];
    applyCalendarHighlights(container, state.dateKeys);
}

export function disposeCalendarEventHighlights(container) {
    if (!container) {
        return;
    }

    const state = calendarObservers.get(container);
    if (!state) {
        return;
    }

    state.observer.disconnect();
    calendarObservers.delete(container);
}