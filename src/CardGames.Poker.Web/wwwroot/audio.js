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
    const SEAT_EXPLOSION_DURATION_MS = 4400;
    const seatParticleCleanupTimeouts = new Map();
    const seatExplosionContainers = new Map();

    function getSeatElement(seatIndex) {
        if (!Number.isFinite(seatIndex)) {
            return null;
        }

        return document.querySelector(`.seat-container[data-seat-index="${seatIndex}"]`);
    }

    function getSeatParticleHost(seatElement) {
        return seatElement?.querySelector(".seat-particle-host") ?? null;
    }

    function destroySeatExplosionContainer(particleHost) {
        if (!particleHost?.id) {
            return;
        }

        const existingContainer = seatExplosionContainers.get(particleHost.id);
        if (!existingContainer) {
            return;
        }

        seatExplosionContainers.delete(particleHost.id);

        try {
            existingContainer.destroy();
        } catch (error) {
            console.warn("Seat loss explosion cleanup failed", error);
        }
    }

    function positionSeatParticleHost(seatElement, particleHost) {
        if (!seatElement || !particleHost) {
            return false;
        }

        const particleAnchor = seatElement.querySelector(".seat-visuals")
            ?? seatElement.querySelector(".info-pill")
            ?? seatElement;

        const hostBounds = particleAnchor.getBoundingClientRect();
        const seatBounds = seatElement.getBoundingClientRect();

        if (hostBounds.width === 0 || hostBounds.height === 0 || seatBounds.width === 0 || seatBounds.height === 0) {
            return false;
        }

        const hostSize = Math.max(170, Math.round(Math.max(hostBounds.width, hostBounds.height) * 1.95));
        const centerX = hostBounds.left - seatBounds.left + (hostBounds.width / 2);
        const centerY = hostBounds.top - seatBounds.top + (hostBounds.height * 0.42);

        particleHost.style.width = `${hostSize}px`;
        particleHost.style.height = `${hostSize}px`;
        particleHost.style.left = `${centerX}px`;
        particleHost.style.top = `${centerY}px`;
        particleHost.style.transform = "translate(-50%, -50%)";

        return true;
    }

    function clearSeatParticleHost(particleHost) {
        if (!particleHost) {
            return;
        }

        const existingTimeout = seatParticleCleanupTimeouts.get(particleHost.id);
        if (existingTimeout) {
            window.clearTimeout(existingTimeout);
        }

        const timeoutId = window.setTimeout(() => {
            seatParticleCleanupTimeouts.delete(particleHost.id);
            destroySeatExplosionContainer(particleHost);
            particleHost.classList.remove("seat-loss-explosion-active");
            particleHost.replaceChildren();
        }, SEAT_EXPLOSION_DURATION_MS + 100);

        seatParticleCleanupTimeouts.set(particleHost.id, timeoutId);
    }

    function triggerSeatLossOverlay(particleHost) {
        if (!particleHost) {
            return;
        }

        particleHost.classList.remove("seat-loss-explosion-active");

        // Restart the CSS animation when multiple losses happen back to back.
        void particleHost.offsetWidth;

        particleHost.classList.add("seat-loss-explosion-active");
    }

    function createSeatLossExplosionOptions() {
        return {
            fullScreen: {
                enable: false,
                zIndex: 0
            },
            background: {
                color: "transparent"
            },
            detectRetina: true,
            fpsLimit: 120,
            pauseOnBlur: false,
            particles: {
                number: {
                    value: 0
                }
            },
            emitters: [
                {
                    position: {
                        x: 50,
                        y: 54
                    },
                    size: {
                        width: 0,
                        height: 0
                    },
                    life: {
                        count: 1,
                        duration: 0.18
                    },
                    rate: {
                        quantity: 32,
                        delay: 0.01
                    },
                    particles: {
                        color: {
                            value: ["#fff7b3", "#ffd166", "#ff9f1c", "#ff6b00", "#ffe8a3"]
                        },
                        shape: {
                            type: ["circle"]
                        },
                        size: {
                            value: {
                                min: 20,
                                max: 42
                            },
                            animation: {
                                enable: true,
                                speed: 48,
                                startValue: "min",
                                destroy: "max"
                            }
                        },
                        opacity: {
                            value: {
                                min: 0.55,
                                max: 1
                            },
                            animation: {
                                enable: true,
                                speed: 2.6,
                                startValue: "max",
                                destroy: "min"
                            }
                        },
                        move: {
                            enable: true,
                            speed: {
                                min: 26,
                                max: 44
                            },
                            outModes: {
                                default: "destroy"
                            },
                            decay: 0.2,
                            gravity: {
                                enable: false
                            }
                        },
                        zIndex: {
                            value: 4
                        }
                    }
                },
                {
                    position: {
                        x: 50,
                        y: 56
                    },
                    size: {
                        width: 0,
                        height: 0
                    },
                    life: {
                        count: 1,
                        duration: 0.42,
                        delay: 0.06
                    },
                    rate: {
                        quantity: 26,
                        delay: 0.018
                    },
                    particles: {
                        color: {
                            value: ["#5b3a29", "#70492d", "#8b5e3c", "#6b7280", "#3f3f46"]
                        },
                        shape: {
                            type: ["square", "circle"]
                        },
                        size: {
                            value: {
                                min: 5,
                                max: 11
                            }
                        },
                        opacity: {
                            value: {
                                min: 0.55,
                                max: 0.9
                            },
                            animation: {
                                enable: true,
                                speed: 1.1,
                                startValue: "max",
                                destroy: "min"
                            }
                        },
                        rotate: {
                            value: {
                                min: 0,
                                max: 360
                            },
                            animation: {
                                enable: true,
                                speed: 120
                            }
                        },
                        move: {
                            enable: true,
                            speed: {
                                min: 7,
                                max: 17
                            },
                            outModes: {
                                default: "destroy"
                            },
                            decay: 0.08,
                            life: {
                                duration: {
                                    value: 1.1,
                                    sync: true
                                }
                            },
                            gravity: {
                                enable: true,
                                acceleration: 1.8
                            },
                            drift: {
                                min: -0.45,
                                max: 0.45
                            }
                        },
                        zIndex: {
                            value: 3
                        }
                    }
                },
                {
                    position: {
                        x: 50,
                        y: 58
                    },
                    size: {
                        width: 0,
                        height: 0
                    },
                    life: {
                        count: 1,
                        duration: 1.9,
                        delay: 0.12
                    },
                    rate: {
                        quantity: 12,
                        delay: 0.07
                    },
                    particles: {
                        color: {
                            value: ["#d1d5db", "#9ca3af", "#6b7280", "#4b5563", "#374151"]
                        },
                        shape: {
                            type: ["circle"]
                        },
                        size: {
                            value: {
                                min: 30,
                                max: 58
                            },
                            animation: {
                                enable: true,
                                speed: 6.5,
                                startValue: "min",
                                destroy: "max"
                            }
                        },
                        opacity: {
                            value: {
                                min: 0.2,
                                max: 0.42
                            },
                            animation: {
                                enable: true,
                                speed: 0.2,
                                startValue: "max",
                                destroy: "min"
                            }
                        },
                        move: {
                            enable: true,
                            speed: {
                                min: 0.45,
                                max: 1.25
                            },
                            direction: "top",
                            outModes: {
                                default: "destroy"
                            },
                            decay: 0.01,
                            life: {
                                duration: {
                                    value: 3.3,
                                    sync: true
                                }
                            },
                            gravity: {
                                enable: false
                            },
                            drift: {
                                min: -0.18,
                                max: 0.18
                            }
                        },
                        zIndex: {
                            value: 2
                        }
                    }
                }
            ]
        };
    }

    async function runSeatLossExplosion(seatIndex) {
        const seatElement = getSeatElement(Number(seatIndex));
        const particleHost = getSeatParticleHost(seatElement);
        const particleHostId = particleHost?.id;

        if (!seatElement || !particleHost || !particleHostId) {
            return;
        }

        if (!positionSeatParticleHost(seatElement, particleHost) || particleHost.offsetWidth === 0 || particleHost.offsetHeight === 0) {
            return;
        }

        if (!window.tsParticles || typeof window.tsParticles.load !== "function") {
            console.warn("Seat loss explosion skipped because tsParticles is unavailable");
            triggerSeatLossOverlay(particleHost);
            clearSeatParticleHost(particleHost);
            return;
        }

        triggerSeatLossOverlay(particleHost);
        destroySeatExplosionContainer(particleHost);
        particleHost.replaceChildren();

        try {
            const explosionContainer = await window.tsParticles.load({
                id: particleHostId,
                options: createSeatLossExplosionOptions()
            });

            seatExplosionContainers.set(particleHostId, explosionContainer);
        } catch (error) {
            console.warn("Seat loss explosion failed to initialize", error);
            destroySeatExplosionContainer(particleHost);
            particleHost.replaceChildren();
            return;
        }

        clearSeatParticleHost(particleHost);
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

    /**
     * Animates a poker chip from a seat to the pot display in the center of the table.
     * Used when a player loses a hand of Screw Your Neighbor.
     * @param {number} seatIndex - The seat index of the losing player.
     * @param {number} [durationMs] - Animation duration in milliseconds.
     * @param {number} [delayMs] - Delay before starting the animation (for staggering multiple losers).
     */
    function animateChipToPot(seatIndex, durationMs, delayMs) {
        const seatElement = getSeatElement(Number(seatIndex));
        const potElement = document.querySelector(".pot-display");
        if (!seatElement || !potElement) {
            return;
        }

        // If the loser has zero stacks, .syn-stacks can exist with width 0.
        // In that case, fall back to other stable seat anchors.
        const candidateAnchors = [
            seatElement.querySelector(".syn-stacks"),
            seatElement.querySelector(".info-pill"),
            seatElement.querySelector(".seat-visuals"),
            seatElement
        ];

        let seatRect = null;
        for (const anchor of candidateAnchors) {
            if (!anchor) {
                continue;
            }

            const rect = anchor.getBoundingClientRect();
            if (rect.width > 0 && rect.height > 0) {
                seatRect = rect;
                break;
            }
        }

        const potRect = potElement.getBoundingClientRect();

        if (!seatRect || !potRect.width) {
            return;
        }

        const resolvedDuration = Number.isFinite(durationMs)
            ? Math.max(200, Math.floor(durationMs))
            : DEFAULT_DURATION_MS;

        const resolvedDelay = Number.isFinite(delayMs) ? Math.max(0, Math.floor(delayMs)) : 0;

        const seatCx = seatRect.left + seatRect.width / 2;
        const seatCy = seatRect.top + seatRect.height / 2;
        const potCx = potRect.left + potRect.width / 2;
        const potCy = potRect.top + potRect.height / 2;

        // Create a synthetic chip — use a styled div with a border that looks like a poker chip.
        // We avoid Font Awesome <i> elements here because dynamically injected icons may not
        // render correctly depending on the FA loading method (CSS font-face timing).
        const chipSize = 26;
        const chip = document.createElement("div");
        chip.style.position = "fixed";
        chip.style.left = `${seatCx - chipSize / 2}px`;
        chip.style.top = `${seatCy - chipSize / 2}px`;
        chip.style.width = `${chipSize}px`;
        chip.style.height = `${chipSize}px`;
        chip.style.borderRadius = "50%";
        chip.style.background = "radial-gradient(circle at 35% 35%, #e53e3e, #991b1b)";
        chip.style.border = "3px solid #fbbf24";
        chip.style.boxShadow = "inset 0 -2px 4px rgba(0,0,0,0.4), 0 2px 8px rgba(0,0,0,0.6)";
        chip.style.transform = "translate3d(0, 0, 0) scale(1)";
        chip.style.pointerEvents = "none";
        chip.style.zIndex = "4000";
        chip.style.opacity = "1";

        document.body.appendChild(chip);

        const dx = potCx - seatCx;
        const dy = potCy - seatCy;

        window.setTimeout(() => {
            window.requestAnimationFrame(() => {
                chip.style.transition = `transform ${resolvedDuration}ms cubic-bezier(0.22, 1, 0.36, 1), opacity ${resolvedDuration}ms ease`;

                window.requestAnimationFrame(() => {
                    chip.style.transform = `translate3d(${dx}px, ${dy}px, 0) scale(0.6)`;
                    chip.style.opacity = "0.7";
                });
            });

            window.setTimeout(() => {
                chip.remove();
            }, resolvedDuration + 100);
        }, resolvedDelay);
    }

    function animateSeatLossExplosion(seatIndex, delayMs) {
        const resolvedDelay = Number.isFinite(delayMs) ? Math.max(0, Math.floor(delayMs)) : 0;

        window.setTimeout(() => {
            void runSeatLossExplosion(Number(seatIndex));
        }, resolvedDelay);
    }

    return {
        animateScrewYourNeighborTrade,
        animateScrewYourNeighborDeckTrade,
        animateChipToPot,
        animateSeatLossExplosion
    };
})();
