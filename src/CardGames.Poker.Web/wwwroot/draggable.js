window.draggablePanel = {
    _storageKey: 'cardGames.panelPosition',

    _getStorageKey(storageKey) {
        return storageKey || this._storageKey;
    },

    /**
     * Initialise drag behaviour on a panel element.
     * @param {HTMLElement} panel      The panel to make draggable (position:fixed).
     * @param {HTMLElement} handle     The drag-handle element (usually the header bar).
     * @param {string} [storageKey]    Optional localStorage key for panel position persistence.
     */
    init(panel, handle, storageKey) {
        if (!panel || !handle) return;
        if (panel._draggableInit) return;   // prevent double-init
        panel._draggableInit = true;

        const self = this;
        const panelStorageKey = self._getStorageKey(storageKey);
        let dragging = false;
        let startX, startY, startLeft, startTop;

        // Try to restore a previously saved position.
        function restoreSavedPosition() {
            try {
                const raw = window.localStorage.getItem(panelStorageKey);
                if (!raw) return false;
                const pos = JSON.parse(raw);
                if (typeof pos.left !== 'number' || typeof pos.top !== 'number') return false;

                panel.style.left   = pos.left + 'px';
                panel.style.top    = pos.top  + 'px';
                panel.style.right  = 'auto';
                panel.style.bottom = 'auto';

                // After applying, clamp in case the window is now smaller.
                clamp(panel);
                return true;
            } catch {
                return false;
            }
        }

        function savePosition() {
            try {
                const rect = panel.getBoundingClientRect();
                window.localStorage.setItem(
                    panelStorageKey,
                    JSON.stringify({ left: rect.left, top: rect.top })
                );
            } catch { /* ignore storage failures */ }
        }

        // Snapshot the panel's initial fixed position so the first drag is seamless.
        function ensurePositionCoords() {
            if (panel.style.left && panel.style.left !== 'auto') return;
            const rect = panel.getBoundingClientRect();
            panel.style.left = rect.left + 'px';
            panel.style.top  = rect.top  + 'px';
            panel.style.right  = 'auto';
            panel.style.bottom = 'auto';
        }

        function clamp(panel) {
            const rect = panel.getBoundingClientRect();
            const vw = window.innerWidth;
            const vh = window.innerHeight;

            let left = rect.left;
            let top  = rect.top;

            if (left < 0) left = 0;
            if (top  < 0) top  = 0;
            if (left + rect.width  > vw) left = vw - rect.width;
            if (top  + rect.height > vh) top  = vh - rect.height;

            panel.style.left = left + 'px';
            panel.style.top  = top  + 'px';
        }

        function onPointerDown(e) {
            // Only primary button
            if (e.button !== 0) return;

            // Don't start drag if user clicked a button or interactive element
            if (e.target.closest('button, input, select, textarea, a')) return;

            ensurePositionCoords();

            dragging = true;
            startX = e.clientX;
            startY = e.clientY;
            startLeft = parseFloat(panel.style.left);
            startTop  = parseFloat(panel.style.top);

            handle.setPointerCapture(e.pointerId);
            panel.classList.add('dragging');
            e.preventDefault();
        }

        function onPointerMove(e) {
            if (!dragging) return;

            const dx = e.clientX - startX;
            const dy = e.clientY - startY;

            panel.style.left = (startLeft + dx) + 'px';
            panel.style.top  = (startTop  + dy) + 'px';

            clamp(panel);
        }

        function onPointerUp(e) {
            if (!dragging) return;
            dragging = false;
            panel.classList.remove('dragging');
            savePosition();

            try { handle.releasePointerCapture(e.pointerId); } catch { /* ok */ }
        }

        handle.addEventListener('pointerdown', onPointerDown);
        handle.addEventListener('pointermove', onPointerMove);
        handle.addEventListener('pointerup',   onPointerUp);
        handle.addEventListener('pointercancel', onPointerUp);

        // Ensure the panel stays within bounds on resize.
        window.addEventListener('resize', () => {
            if (panel.style.left && panel.style.left !== 'auto') {
                clamp(panel);
                savePosition();
            }
        });

        // Also handle touch events to prevent page scroll while dragging
        handle.style.touchAction = 'none';

        // Restore saved position (shared across all panel types).
        restoreSavedPosition();
    }
};
