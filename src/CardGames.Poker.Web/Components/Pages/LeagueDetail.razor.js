function getSupportedTimeZones() {
    if (typeof Intl.supportedValuesOf === 'function') {
        return Intl.supportedValuesOf('timeZone');
    }

    return [Intl.DateTimeFormat().resolvedOptions().timeZone];
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