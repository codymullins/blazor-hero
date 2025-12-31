// Device Helpers - Detection and viewport management for mobile support

let dotNetReference = null;
let resizeTimeout = null;

/**
 * Check if the device is a mobile device based on user agent and touch capability.
 */
export function isMobileDevice() {
    const userAgentMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
    const hasTouch = navigator.maxTouchPoints > 0 || 'ontouchstart' in window;
    const isSmallScreen = window.innerWidth <= 1024;
    
    // Consider it mobile if user agent says so, or if it's a touch device with small screen
    return userAgentMobile || (hasTouch && isSmallScreen);
}

/**
 * Check if the device has touch capability.
 */
export function hasTouch() {
    return navigator.maxTouchPoints > 0 || 'ontouchstart' in window;
}

/**
 * Get current viewport dimensions.
 */
export function getViewportSize() {
    return {
        width: window.innerWidth,
        height: window.innerHeight
    };
}

/**
 * Get current orientation.
 */
export function getOrientation() {
    if (screen.orientation) {
        return screen.orientation.type.includes('portrait') ? 'portrait' : 'landscape';
    }
    // Fallback for older browsers
    return window.innerWidth > window.innerHeight ? 'landscape' : 'portrait';
}

/**
 * Initialize event listeners for viewport and orientation changes.
 */
export function initEventListeners(reference) {
    dotNetReference = reference;

    // Debounced resize handler
    window.addEventListener('resize', handleResize);

    // Orientation change handler
    if (screen.orientation) {
        screen.orientation.addEventListener('change', handleOrientationChange);
    } else {
        window.addEventListener('orientationchange', handleOrientationChange);
    }
}

/**
 * Clean up event listeners.
 */
export function disposeEventListeners() {
    window.removeEventListener('resize', handleResize);
    
    if (screen.orientation) {
        screen.orientation.removeEventListener('change', handleOrientationChange);
    } else {
        window.removeEventListener('orientationchange', handleOrientationChange);
    }
    
    dotNetReference = null;
    
    if (resizeTimeout) {
        clearTimeout(resizeTimeout);
    }
}

function handleResize() {
    // Debounce resize events
    if (resizeTimeout) {
        clearTimeout(resizeTimeout);
    }
    
    resizeTimeout = setTimeout(() => {
        if (dotNetReference) {
            dotNetReference.invokeMethodAsync('OnViewportChange', window.innerWidth, window.innerHeight);
        }
    }, 100);
}

function handleOrientationChange() {
    if (dotNetReference) {
        const orientation = getOrientation();
        dotNetReference.invokeMethodAsync('OnOrientationChange', orientation);
    }
}

/**
 * Attempt to lock screen orientation (requires fullscreen on most browsers).
 */
export async function tryLockOrientation(orientation) {
    try {
        if (screen.orientation && screen.orientation.lock) {
            await screen.orientation.lock(orientation);
            return true;
        }
    } catch (e) {
        console.log('[DeviceHelpers] Orientation lock not supported:', e.message);
    }
    return false;
}

/**
 * Request fullscreen mode.
 */
export async function tryRequestFullscreen() {
    try {
        const elem = document.documentElement;
        if (elem.requestFullscreen) {
            await elem.requestFullscreen();
            return true;
        } else if (elem.webkitRequestFullscreen) {
            await elem.webkitRequestFullscreen();
            return true;
        } else if (elem.mozRequestFullScreen) {
            await elem.mozRequestFullScreen();
            return true;
        }
    } catch (e) {
        console.log('[DeviceHelpers] Fullscreen not supported:', e.message);
    }
    return false;
}

/**
 * Exit fullscreen mode.
 */
export async function exitFullscreen() {
    try {
        if (document.exitFullscreen) {
            await document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
            await document.webkitExitFullscreen();
        } else if (document.mozCancelFullScreen) {
            await document.mozCancelFullScreen();
        }
    } catch (e) {
        // Ignore errors
    }
}

/**
 * Check if currently in fullscreen mode.
 */
export function isFullscreen() {
    return !!(document.fullscreenElement || document.webkitFullscreenElement || document.mozFullScreenElement);
}
