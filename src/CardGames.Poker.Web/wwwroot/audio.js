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

window.cardGamesTable = (() => {
    const DEFAULT_DURATION_MS = 650;

    function getSeatElement(seatIndex) {
        if (!Number.isFinite(seatIndex)) {
            return null;
        }

        return document.querySelector(`.seat-container[data-seat-index="${seatIndex}"]`);
    }

    /**
     * Gets the bounding rect of a seat's card area.
     * Falls back to the seat container itself if no cards are rendered yet.
     */
    function getSeatCardRect(seatElement) {
        if (!seatElement) {
            return null;
        }

        // Try the cards-fan container first (where cards live)
        const cardsFan = seatElement.querySelector(".cards-fan");
        if (cardsFan) {
            const rect = cardsFan.getBoundingClientRect();
            if (rect.width > 0 && rect.height > 0) {
                return rect;
            }
        }

        // Try any playing-card element
        const playingCard = seatElement.querySelector("playing-card");
        if (playingCard) {
            const rect = playingCard.getBoundingClientRect();
            if (rect.width > 0 && rect.height > 0) {
                return rect;
            }
        }

        // Last resort: use the seat-visuals (avatar area)
        const visuals = seatElement.querySelector(".seat-visuals");
        if (visuals) {
            return visuals.getBoundingClientRect();
        }

        return seatElement.getBoundingClientRect();
    }

    /**
     * Creates a synthetic face-down playing card element for animation.
     * Does NOT depend on any existing DOM card state — created from scratch.
     */
    function createSyntheticCard(centerX, centerY) {
        const cardW = 50;
        const cardH = 70;
        const card = document.createElement("playing-card");
        card.setAttribute("rank", "0");
        card.setAttribute("backcolor", "#44F");

        card.style.position = "fixed";
        card.style.left = `${centerX - cardW / 2}px`;
        card.style.top = `${centerY - cardH / 2}px`;
        card.style.width = `${cardW}px`;
        card.style.height = `${cardH}px`;
        card.style.margin = "0";
        card.style.transform = "translate3d(0, 0, 0)";
        card.style.pointerEvents = "none";
        card.style.zIndex = "4000";
        card.style.opacity = "1";
        card.style.filter = "drop-shadow(0 4px 12px rgba(0,0,0,0.5))";

        document.body.appendChild(card);
        return card;
    }

    function animateScrewYourNeighborTrade(sourceSeatIndex, targetSeatIndex, durationMs) {
        const sourceSeat = getSeatElement(Number(sourceSeatIndex));
        const targetSeat = getSeatElement(Number(targetSeatIndex));
        if (!sourceSeat || !targetSeat) {
            return;
        }

        const sourceRect = getSeatCardRect(sourceSeat);
        const targetRect = getSeatCardRect(targetSeat);
        if (!sourceRect || !targetRect) {
            return;
        }

        const resolvedDuration = Number.isFinite(durationMs)
            ? Math.max(200, Math.floor(durationMs))
            : DEFAULT_DURATION_MS;

        // Compute center points of each seat's card area
        const sourceCx = sourceRect.left + sourceRect.width / 2;
        const sourceCy = sourceRect.top + sourceRect.height / 2;
        const targetCx = targetRect.left + targetRect.width / 2;
        const targetCy = targetRect.top + targetRect.height / 2;

        // Create two fresh face-down cards positioned at each seat center
        const sourceCard = createSyntheticCard(sourceCx, sourceCy);
        const targetCard = createSyntheticCard(targetCx, targetCy);

        const dx = targetCx - sourceCx;
        const dy = targetCy - sourceCy;

        // Double-rAF: first frame commits the initial position, second starts the transition
        window.requestAnimationFrame(() => {
            sourceCard.style.transition = `transform ${resolvedDuration}ms cubic-bezier(0.22, 1, 0.36, 1)`;
            targetCard.style.transition = sourceCard.style.transition;

            window.requestAnimationFrame(() => {
                sourceCard.style.transform = `translate3d(${dx}px, ${dy}px, 0) rotate(8deg)`;
                targetCard.style.transform = `translate3d(${-dx}px, ${-dy}px, 0) rotate(-8deg)`;
            });
        });

        // Clean up after animation completes
        window.setTimeout(() => {
            sourceCard.remove();
            targetCard.remove();
        }, resolvedDuration + 100);
    }

    /**
     * Animates a dealer trading with the deck.
     * A single card flies from the deck to the dealer's seat.
     */
    function animateScrewYourNeighborDeckTrade(dealerSeatIndex, durationMs) {
        const deckElement = document.querySelector(".deck-stack");
        const dealerSeat = getSeatElement(Number(dealerSeatIndex));
        if (!deckElement || !dealerSeat) {
            return;
        }

        const deckRect = deckElement.getBoundingClientRect();
        const dealerRect = getSeatCardRect(dealerSeat);
        if (!deckRect.width || !dealerRect) {
            return;
        }

        const resolvedDuration = Number.isFinite(durationMs)
            ? Math.max(200, Math.floor(durationMs))
            : DEFAULT_DURATION_MS;

        const deckCx = deckRect.left + deckRect.width / 2;
        const deckCy = deckRect.top + deckRect.height / 2;
        const dealerCx = dealerRect.left + dealerRect.width / 2;
        const dealerCy = dealerRect.top + dealerRect.height / 2;

        // Card starts at the deck and flies to the dealer
        const card = createSyntheticCard(deckCx, deckCy);

        const dx = dealerCx - deckCx;
        const dy = dealerCy - deckCy;

        window.requestAnimationFrame(() => {
            card.style.transition = `transform ${resolvedDuration}ms cubic-bezier(0.22, 1, 0.36, 1)`;

            window.requestAnimationFrame(() => {
                card.style.transform = `translate3d(${dx}px, ${dy}px, 0) rotate(-6deg)`;
            });
        });

        window.setTimeout(() => {
            card.remove();
        }, resolvedDuration + 100);
    }

    return {
        animateScrewYourNeighborTrade,
        animateScrewYourNeighborDeckTrade
    };
})();
