// Game Loop - requestAnimationFrame based game loop with C# interop

let gameInstance = null;
let isRunning = false;
let lastTimestamp = 0;

export function initGameLoop(dotNetReference) {
    gameInstance = dotNetReference;
    isRunning = true;
    lastTimestamp = performance.now();
    requestAnimationFrame(gameLoop);
}

function gameLoop(timestamp) {
    if (!isRunning || !gameInstance) return;

    const deltaTime = timestamp - lastTimestamp;
    lastTimestamp = timestamp;

    // Cap delta to prevent spiral of death on lag spikes (max ~30fps equivalent)
    const cappedDelta = Math.min(deltaTime, 33.33);

    // Sync interop for performance - C# method is still async but JS doesn't await
    gameInstance.invokeMethod('OnFrame', cappedDelta, timestamp);
    requestAnimationFrame(gameLoop);
}

export function stopGameLoop() {
    isRunning = false;
    gameInstance = null;
}

export function isGameLoopRunning() {
    return isRunning;
}

export function pauseGameLoop() {
    isRunning = false;
}

export function resumeGameLoop() {
    if (gameInstance && !isRunning) {
        isRunning = true;
        lastTimestamp = performance.now();
        requestAnimationFrame(gameLoop);
    }
}
