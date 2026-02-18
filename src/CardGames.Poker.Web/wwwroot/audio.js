window.cardGamesAudio = (() => {
    const dealSoundMutedStorageKey = "cardGames.dealSoundMuted";
    let preferredDealCardSource;
    let isMuted = false;

    try {
        const persisted = window.localStorage.getItem(dealSoundMutedStorageKey);
        if (persisted !== null) {
            isMuted = persisted === "true";
        }
    } catch {
        // Ignore storage access failures.
    }

    function resolveDealCardSource() {
        if (preferredDealCardSource) {
            return preferredDealCardSource;
        }

        const audio = document.createElement("audio");
        const canPlayOgg = typeof audio.canPlayType === "function"
            && audio.canPlayType('audio/ogg; codecs="vorbis"') !== "";

        preferredDealCardSource = canPlayOgg ? "/sounds/dealcard.ogg" : "/sounds/dealcard.mp3";
        return preferredDealCardSource;
    }

    function playDealCard(count) {
        if (isMuted) {
            return;
        }

        const cardsToPlay = Number.isFinite(count) ? Math.max(1, Math.floor(count)) : 1;
        const source = resolveDealCardSource();

        for (let index = 0; index < cardsToPlay; index += 1) {
            window.setTimeout(() => {
                const effect = new Audio(source);
                effect.preload = "auto";
                void effect.play().catch(() => {
                    // Ignore browser autoplay/capability failures.
                });
            }, index * 90);
        }
    }

    function setMuted(muted) {
        isMuted = Boolean(muted);

        try {
            window.localStorage.setItem(dealSoundMutedStorageKey, isMuted ? "true" : "false");
        } catch {
            // Ignore storage access failures.
        }
    }

    function getMuted() {
        return isMuted;
    }

    return {
        playDealCard,
        setMuted,
        getMuted
    };
})();
