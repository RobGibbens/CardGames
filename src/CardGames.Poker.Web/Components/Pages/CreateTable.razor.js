const flipStateByRoot = new WeakMap();

function getVariantCards(root) {
    if (!root) {
        return [];
    }

    return Array.from(root.querySelectorAll('.variant-card[data-variant-code]'));
}

function getCardCode(card) {
    return card.getAttribute('data-variant-code') || '';
}

export function captureVariantSelectionFlip(root) {
    const cards = getVariantCards(root);
    const firstRects = new Map();

    for (const card of cards) {
        const code = getCardCode(card);
        if (!code) {
            continue;
        }

        firstRects.set(code, card.getBoundingClientRect());
    }

    flipStateByRoot.set(root, firstRects);
}

export function playVariantSelectionFlip(root) {
    const firstRects = flipStateByRoot.get(root);
    if (!firstRects || firstRects.size === 0) {
        return;
    }

    const cards = getVariantCards(root);

    for (const card of cards) {
        const code = getCardCode(card);
        if (!code) {
            continue;
        }

        const first = firstRects.get(code);
        if (!first) {
            continue;
        }

        const last = card.getBoundingClientRect();
        if (last.width <= 0 || last.height <= 0) {
            continue;
        }

        const deltaX = first.left - last.left;
        const deltaY = first.top - last.top;
        const scaleX = first.width / last.width;
        const scaleY = first.height / last.height;

        card.style.transition = 'none';
        card.style.transformOrigin = 'top left';
        card.style.transform = `translate(${deltaX}px, ${deltaY}px) scale(${scaleX}, ${scaleY})`;

        const targetOpacity = getComputedStyle(card).opacity;
        if (targetOpacity !== '1') {
            card.style.opacity = '1';
        }

        card.getBoundingClientRect();

        requestAnimationFrame(() => {
            card.style.transition = 'transform 560ms cubic-bezier(0.22, 1, 0.36, 1), opacity 420ms ease';
            card.style.transform = '';
            card.style.opacity = '';

            const cleanup = () => {
                card.style.transition = '';
                card.style.transformOrigin = '';
                card.removeEventListener('transitionend', cleanup);
            };

            card.addEventListener('transitionend', cleanup);
        });
    }

    flipStateByRoot.delete(root);
}
