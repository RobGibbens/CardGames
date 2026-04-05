/**
 * playing-card-images.js
 *
 * Drop-in replacement for the Cardmeister <playing-card> custom element.
 * Renders PNG card images from /images/cards/ instead of generated SVG.
 *
 * Face-down: rank="0" or missing rank/suit  →  back-blue-white.png
 * Face-up:   rank + suit present            →  {suit}-{rank}.png
 *
 * Observed attributes (same contract as Cardmeister):
 *   rank, suit, class, style, opacity,
 *   backcolor, suitcolor, rankcolor, courtcolor
 *
 * When suitcolor/rankcolor/courtcolor are set (wild card highlighting),
 * the host element gets a "card-wild-highlight" CSS class so styling
 * can add a glow/ring without recoloring the PNG.
 */
(function () {
    "use strict";

    const CARD_IMAGE_BASE = "/images/cards/";
    const BACK_IMAGE = CARD_IMAGE_BASE + "back-blue-white.png";
    const UNKNOWN_IMAGE = CARD_IMAGE_BASE + "unknown.png";

    /**
     * Normalize a rank value (from any of the formats used across the app)
     * into the lowercase word used in PNG filenames.
     */
    function normalizeRank(rank) {
        if (rank == null) return null;
        var r = String(rank).trim().toLowerCase();
        switch (r) {
            case "1":
            case "a":
            case "ace":
                return "ace";
            case "2":
            case "two":
            case "deuce":
                return "two";
            case "3":
            case "three":
                return "three";
            case "4":
            case "four":
                return "four";
            case "5":
            case "five":
                return "five";
            case "6":
            case "six":
                return "six";
            case "7":
            case "seven":
                return "seven";
            case "8":
            case "eight":
                return "eight";
            case "9":
            case "nine":
                return "nine";
            case "10":
            case "t":
            case "ten":
                return "ten";
            case "11":
            case "j":
            case "jack":
                return "jack";
            case "12":
            case "q":
            case "queen":
                return "queen";
            case "13":
            case "k":
            case "king":
                return "king";
            default:
                return null; // unrecognised → face-down
        }
    }

    /**
     * Normalize a suit string into the lowercase word used in PNG filenames.
     */
    function normalizeSuit(suit) {
        if (suit == null) return null;
        var s = String(suit).trim().toLowerCase();
        switch (s) {
            case "hearts":
            case "h":
                return "hearts";
            case "diamonds":
            case "d":
                return "diamonds";
            case "clubs":
            case "c":
                return "clubs";
            case "spades":
            case "s":
                return "spades";
            default:
                return null;
        }
    }

    /**
     * Determine whether the card should show a face (front image) or
     * the back image, and return the appropriate image URL.
     */
    function resolveImageUrl(rankAttr, suitAttr) {
        // rank="0" is the Cardmeister convention for face-down
        if (rankAttr === "0" || rankAttr === 0) return BACK_IMAGE;

        var rank = normalizeRank(rankAttr);
        var suit = normalizeSuit(suitAttr);

        if (rank && suit) {
            return CARD_IMAGE_BASE + suit + "-" + rank + ".png";
        }
        return BACK_IMAGE;
    }

    /**
     * Check whether any wild-card tint attribute is set.
     */
    function hasWildHighlight(el) {
        return (
            el.getAttribute("suitcolor") ||
            el.getAttribute("rankcolor") ||
            el.getAttribute("courtcolor")
        );
    }

    // ---------------------------------------------------------------
    // Custom Element Definition
    // ---------------------------------------------------------------
    if (customElements.get("playing-card")) return; // don't double-register

    customElements.define(
        "playing-card",
        class extends HTMLElement {
            static get observedAttributes() {
                return [
                    "rank",
                    "suit",
                    "backcolor",
                    "suitcolor",
                    "rankcolor",
                    "courtcolor",
                    "opacity",
                ];
            }

            constructor() {
                super();
                this._img = null;
            }

            connectedCallback() {
                this.style.display = "inline-block";
                this._render();
            }

            attributeChangedCallback(_name, oldVal, newVal) {
                if (oldVal !== newVal) {
                    this._render();
                }
            }

            _render() {
                var rank = this.getAttribute("rank");
                var suit = this.getAttribute("suit");
                var src = resolveImageUrl(rank, suit);

                if (!this._img) {
                    this._img = document.createElement("img");
                    this._img.style.width = "100%";
                    this._img.style.height = "100%";
                    this._img.style.display = "block";
                    this._img.style.objectFit = "fill";
                    this._img.style.borderRadius = "inherit";
                    this._img.style.pointerEvents = "none";
                    this._img.draggable = false;
                    this._img.setAttribute("alt", "");
                    this._img.addEventListener("error", function () {
                        if (
                            this.dataset.imageKind !== "front" ||
                            this.dataset.fallbackApplied === "true"
                        ) {
                            return;
                        }

                        this.dataset.fallbackApplied = "true";
                        this.setAttribute("src", UNKNOWN_IMAGE);
                    });
                    this.appendChild(this._img);
                }

                this._img.dataset.imageKind = src === BACK_IMAGE ? "back" : "front";
                this._img.dataset.fallbackApplied = "false";

                if (this._img.getAttribute("src") !== src) {
                    this._img.setAttribute("src", src);
                }

                // Opacity forwarding
                var opacity = this.getAttribute("opacity");
                if (opacity && opacity !== "1") {
                    this.style.opacity = opacity;
                }

                // Wild-card highlight class
                if (hasWildHighlight(this)) {
                    this.classList.add("card-wild-highlight");
                } else {
                    this.classList.remove("card-wild-highlight");
                }
            }
        }
    );
})();
