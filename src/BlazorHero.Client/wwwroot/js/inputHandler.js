// Input Handler - Keyboard event handling with C# interop

let dotNetReference = null;
const keyState = new Map();

// Key mappings for lanes (5 lanes for Expert mode)
const KEY_BINDINGS = {
    'KeyD': 0, // Green
    'KeyF': 1, // Red
    'KeyJ': 2, // Yellow
    'KeyK': 3, // Blue
    'KeyL': 4  // Orange (Expert only)
};

const SPECIAL_KEYS = {
    'Escape': 'escape',
    'Enter': 'confirm',
    'Space': 'starpower',
    'ArrowUp': 'up',
    'ArrowDown': 'down',
    'ArrowLeft': 'left',
    'ArrowRight': 'right',
    'KeyR': 'restart',
    'Backspace': 'back'
};

export function initInput(reference) {
    dotNetReference = reference;

    document.addEventListener('keydown', handleKeyDown);
    document.addEventListener('keyup', handleKeyUp);
}

export function disposeInput() {
    document.removeEventListener('keydown', handleKeyDown);
    document.removeEventListener('keyup', handleKeyUp);
    dotNetReference = null;
    keyState.clear();
}

function handleKeyDown(event) {
    if (!dotNetReference) {
        console.log('[InputHandler] No dotNetReference, ignoring key:', event.code);
        return;
    }
    if (event.repeat) return; // Ignore key repeat

    const lane = KEY_BINDINGS[event.code];
    if (lane !== undefined) {
        event.preventDefault();
        if (!keyState.get(event.code)) {
            keyState.set(event.code, true);
            const timestamp = performance.now();
            dotNetReference.invokeMethod('OnLaneKeyDown', lane, timestamp);
        }
        return;
    }

    const special = SPECIAL_KEYS[event.code];
    if (special) {
        event.preventDefault();
        console.log('[InputHandler] Special key:', special);
        dotNetReference.invokeMethod('OnSpecialKey', special);
    }
}

function handleKeyUp(event) {
    if (!dotNetReference) return;

    const lane = KEY_BINDINGS[event.code];
    if (lane !== undefined) {
        keyState.set(event.code, false);
        const timestamp = performance.now();
        dotNetReference.invokeMethod('OnLaneKeyUp', lane, timestamp);
    }
}

export function isKeyPressed(keyCode) {
    return keyState.get(keyCode) || false;
}

export function getLaneStates() {
    return [
        keyState.get('KeyD') || false,
        keyState.get('KeyF') || false,
        keyState.get('KeyJ') || false,
        keyState.get('KeyK') || false,
        keyState.get('KeyL') || false
    ];
}
