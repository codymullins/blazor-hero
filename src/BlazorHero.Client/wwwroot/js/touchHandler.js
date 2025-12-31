// Touch Handler - Multi-touch input handling for mobile gameplay

let dotNetReference = null;
const activeTouches = new Map(); // touchId -> { lane, element }

// Touch control elements will be registered here
let touchControlsContainer = null;
let starPowerButton = null;
let pauseButton = null;

// Lane boundaries for coordinate-based detection
// Array of X positions: [lane0_left, lane1_left, lane2_left, lane3_left, lane4_left, lane4_right]
let laneBoundaries = [];
let touchOverlayRect = null;

/**
 * Initialize touch input handling.
 */
export function initTouch(reference) {
    dotNetReference = reference;
    
    // Prevent default touch behaviors on the game container
    document.addEventListener('touchstart', preventDefaultOnGame, { passive: false });
    document.addEventListener('touchmove', preventDefaultOnGame, { passive: false });
    
    console.log('[TouchHandler] Initialized');
}

/**
 * Dispose touch handling.
 */
export function disposeTouch() {
    document.removeEventListener('touchstart', preventDefaultOnGame);
    document.removeEventListener('touchmove', preventDefaultOnGame);

    if (touchControlsContainer) {
        touchControlsContainer.removeEventListener('touchstart', handleTouchStart);
        touchControlsContainer.removeEventListener('touchend', handleTouchEnd);
        touchControlsContainer.removeEventListener('touchcancel', handleTouchEnd);
    }

    dotNetReference = null;
    activeTouches.clear();
    laneBoundaries = [];
    touchOverlayRect = null;
    starPowerButton = null;
    pauseButton = null;
    touchControlsContainer = null;

    console.log('[TouchHandler] Disposed');
}

/**
 * Set lane boundaries for coordinate-based lane detection.
 * @param {number[]} boundaries - Array of X positions defining lane edges
 */
export function setLaneBoundaries(boundaries) {
    laneBoundaries = boundaries || [];
    console.log('[TouchHandler] Lane boundaries set:', laneBoundaries);
}

/**
 * Update the cached overlay rect (call after resize).
 */
export function updateOverlayRect() {
    if (touchControlsContainer) {
        touchOverlayRect = touchControlsContainer.getBoundingClientRect();
    }
}

/**
 * Get lane index from X coordinate.
 * @param {number} clientX - Touch X position in viewport coordinates
 * @returns {number} Lane index (0-4) or -1 if outside lanes
 */
function getLaneFromX(clientX) {
    if (laneBoundaries.length < 2) {
        console.log('[TouchHandler] getLaneFromX: No boundaries set');
        return -1;
    }

    // Convert viewport X to container-relative X
    // The lane boundaries are in canvas coordinates, and the overlay covers the canvas
    let relativeX = clientX;
    if (touchOverlayRect) {
        relativeX = clientX - touchOverlayRect.left;
    }

    // Debug logging
    console.log(`[TouchHandler] clientX=${clientX}, relativeX=${relativeX}, boundaries=[${laneBoundaries.map(b => b.toFixed(0)).join(', ')}]`);

    // Find which lane the X coordinate falls into
    for (let i = 0; i < laneBoundaries.length - 1; i++) {
        if (relativeX >= laneBoundaries[i] && relativeX < laneBoundaries[i + 1]) {
            console.log(`[TouchHandler] Detected lane ${i}`);
            return i;
        }
    }

    console.log('[TouchHandler] Touch outside lanes');
    return -1; // Outside lane boundaries
}

/**
 * Register touch controls container element.
 * Called from Blazor when TouchControls component mounts.
 */
export function registerTouchControls(containerId) {
    touchControlsContainer = document.getElementById(containerId);

    if (!touchControlsContainer) {
        console.error('[TouchHandler] Container not found:', containerId);
        return;
    }

    // Cache the overlay rect for coordinate translation
    touchOverlayRect = touchControlsContainer.getBoundingClientRect();

    // Find special buttons (star power and pause at edges)
    starPowerButton = touchControlsContainer.querySelector('[data-action="starpower"]');
    pauseButton = touchControlsContainer.querySelector('[data-action="pause"]');

    // Add touch listeners to the container
    touchControlsContainer.addEventListener('touchstart', handleTouchStart, { passive: false });
    touchControlsContainer.addEventListener('touchend', handleTouchEnd, { passive: false });
    touchControlsContainer.addEventListener('touchcancel', handleTouchEnd, { passive: false });

    console.log('[TouchHandler] Registered touch overlay, boundaries:', laneBoundaries.length);
}

/**
 * Unregister touch controls (called when component unmounts).
 */
export function unregisterTouchControls() {
    if (touchControlsContainer) {
        touchControlsContainer.removeEventListener('touchstart', handleTouchStart);
        touchControlsContainer.removeEventListener('touchend', handleTouchEnd);
        touchControlsContainer.removeEventListener('touchcancel', handleTouchEnd);
    }

    activeTouches.clear();
    touchOverlayRect = null;
    starPowerButton = null;
    pauseButton = null;
    touchControlsContainer = null;
}

/**
 * Prevent default touch behaviors on game elements to avoid scrolling/zooming.
 */
function preventDefaultOnGame(event) {
    // Only prevent default if touching game-related elements
    const target = event.target;
    if (target.closest('.game-screen') || target.closest('.touch-controls')) {
        event.preventDefault();
    }
}

/**
 * Handle touch start events.
 */
function handleTouchStart(event) {
    if (!dotNetReference) return;

    const touches = event.changedTouches;
    const timestamp = performance.now();

    for (let i = 0; i < touches.length; i++) {
        const touch = touches[i];
        const target = document.elementFromPoint(touch.clientX, touch.clientY);

        if (!target) continue;

        // Check if touching star power button
        const starButton = target.closest('[data-action="starpower"]');
        if (starButton) {
            dotNetReference.invokeMethod('OnTouchSpecialKey', 'starpower');
            starButton.classList.add('pressed');
            activeTouches.set(touch.identifier, { action: 'starpower', element: starButton });
            event.preventDefault();
            continue;
        }

        // Check if touching pause button
        const pauseBtn = target.closest('[data-action="pause"]');
        if (pauseBtn) {
            dotNetReference.invokeMethod('OnTouchSpecialKey', 'pause');
            pauseBtn.classList.add('pressed');
            activeTouches.set(touch.identifier, { action: 'pause', element: pauseBtn });
            event.preventDefault();
            continue;
        }

        // Coordinate-based lane detection for the touch overlay area
        const lane = getLaneFromX(touch.clientX);
        if (lane >= 0 && lane < 5) {
            // Store this touch
            activeTouches.set(touch.identifier, { lane, element: null });

            // Add visual feedback to lane zone if it exists
            const laneZone = touchControlsContainer?.querySelector(`.lane-zone[data-lane="${lane}"]`);
            if (laneZone) {
                laneZone.classList.add('pressed');
            }

            // Notify .NET
            dotNetReference.invokeMethod('OnTouchLaneDown', lane, timestamp);

            event.preventDefault();
        }
    }
}

/**
 * Handle touch end/cancel events.
 */
function handleTouchEnd(event) {
    if (!dotNetReference) return;

    const touches = event.changedTouches;
    const timestamp = performance.now();

    for (let i = 0; i < touches.length; i++) {
        const touch = touches[i];
        const touchInfo = activeTouches.get(touch.identifier);

        if (touchInfo) {
            // Remove visual feedback
            if (touchInfo.element) {
                touchInfo.element.classList.remove('pressed');
            }

            // If it was a lane touch, notify .NET and remove lane zone feedback
            if (touchInfo.lane !== undefined) {
                dotNetReference.invokeMethod('OnTouchLaneUp', touchInfo.lane, timestamp);

                // Remove visual feedback from lane zone
                const laneZone = touchControlsContainer?.querySelector(`.lane-zone[data-lane="${touchInfo.lane}"]`);
                if (laneZone) {
                    laneZone.classList.remove('pressed');
                }
            }

            // Clean up
            activeTouches.delete(touch.identifier);
        }
    }
}

/**
 * Get current state of all lanes (for polling if needed).
 */
export function getTouchLaneStates() {
    const states = [false, false, false, false, false];
    
    for (const [, touchInfo] of activeTouches) {
        if (touchInfo.lane !== undefined && touchInfo.lane >= 0 && touchInfo.lane < 5) {
            states[touchInfo.lane] = true;
        }
    }
    
    return states;
}

/**
 * Clear all active touches (useful when game pauses/ends).
 */
export function clearActiveTouches() {
    for (const [, touchInfo] of activeTouches) {
        if (touchInfo.element) {
            touchInfo.element.classList.remove('pressed');
        }
    }
    activeTouches.clear();
}
