// Dashboard Panel Resize Module
// Handles drag-to-resize functionality with localStorage persistence

const STORAGE_KEY = 'dashboardPanelWidth';
const MIN_WIDTH = 300;
const MAX_WIDTH = 800;
const DEFAULT_WIDTH = 560;
const VERTICAL_TOLERANCE = 30; // pixels of vertical movement allowed while dragging

let isDragging = false;
let startX = 0;
let startWidth = 0;
let panelElement = null;
let dotNetRef = null;

/**
 * Initialize the resize functionality for the dashboard panel
 * @param {HTMLElement} panel - The dashboard panel element
 * @param {HTMLElement} handle - The resize handle element
 * @param {object} dotNetReference - Reference to the Blazor component for callbacks
 */
export function initResize(panel, handle, dotNetReference) {
    panelElement = panel;
    dotNetRef = dotNetReference;

    handle.addEventListener('mousedown', onMouseDown);
    
    // Cleanup function - will be called when component disposes
    return {
        dispose: () => {
            handle.removeEventListener('mousedown', onMouseDown);
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        }
    };
}

function onMouseDown(e) {
    if (window.innerWidth <= 768) {
        return; // Disable resize on mobile
    }
    
    isDragging = true;
    startX = e.clientX;
    startWidth = panelElement.offsetWidth;
    
    // Prevent text selection while dragging
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'ew-resize';
    
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
    
    e.preventDefault();
}

function onMouseMove(e) {
    if (!isDragging) return;
    
    // Calculate vertical deviation from start point
    const deltaY = Math.abs(e.clientY - e.clientY); // This would need startY tracking
    
    // Calculate horizontal movement (panel expands to the right, so positive deltaX = wider)
    const deltaX = e.clientX - startX;
    let newWidth = startWidth + deltaX;
    
    // Clamp to min/max bounds
    newWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, newWidth));
    
    // Apply the width directly during drag for smooth feedback
    panelElement.style.width = `${newWidth}px`;
}

function onMouseUp(e) {
    if (!isDragging) return;
    
    isDragging = false;
    
    // Restore normal cursor and selection
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
    
    document.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('mouseup', onMouseUp);
    
    // Get the final width
    const finalWidth = panelElement.offsetWidth;
    
    // Save to localStorage
    setStoredWidth(finalWidth);
    
    // Notify Blazor component of the new width
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('OnResizeComplete', finalWidth);
    }
}

/**
 * Get the stored width from localStorage
 * @returns {number} The stored width or default value
 */
export function getStoredWidth() {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
        const width = parseInt(stored, 10);
        if (!isNaN(width) && width >= MIN_WIDTH && width <= MAX_WIDTH) {
            return width;
        }
    }
    return DEFAULT_WIDTH;
}

/**
 * Save width to localStorage
 * @param {number} width - The width to store
 */
export function setStoredWidth(width) {
    const clampedWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, width));
    localStorage.setItem(STORAGE_KEY, clampedWidth.toString());
}

/**
 * Apply width to the panel element
 * @param {HTMLElement} panel - The dashboard panel element
 * @param {number} width - The width to apply
 */
export function applyWidth(panel, width) {
    if (panel) {
        const clampedWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, width));
        panel.style.width = `${clampedWidth}px`;
    }
}

