import { dotnet } from './_framework/dotnet.js';
const status = document.getElementById('status');
// Sample-only measurement: verify actual PCM output reaches the WebAudio graph.
let peak = 0, currentPeak = 0, callbacks = 0, failure;
const contextType = globalThis.AudioContext || globalThis.webkitAudioContext;
const original = contextType.prototype.createScriptProcessor;
contextType.prototype.createScriptProcessor = function (...args) {
    const node = original.apply(this, args);
    // Register after miniaudio assigns onaudioprocess so we see the filled buffer.
    queueMicrotask(() => node.addEventListener('audioprocess', event => {
        callbacks++;
        currentPeak = 0;
        const data = event.outputBuffer.getChannelData(0);
        for (const value of data) currentPeak = Math.max(currentPeak, Math.abs(value));
        peak = Math.max(peak, currentPeak);
    }));
    return node;
};
try {
    const runtime = await dotnet.create();
    const { AudioDemo: app } = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    await runtime.runMain();
    for (const [id, method] of Object.entries({play:'Play', stream:'Stream', pause:'Pause', seek:'Seek', stop:'Stop', start:'Start'})) {
        const button = document.getElementById(id);
        button.disabled = false;
        button.onclick = () => { try { app[method](); } catch (error) { failure = String(error); } };
    }
    setInterval(() => {
        status.textContent = failure ?? app.Status();
        document.getElementById('signal').textContent = `Audio callbacks: ${callbacks} | Current peak: ${currentPeak.toFixed(5)} | Maximum peak: ${peak.toFixed(5)}`;
    }, 250);
} catch (error) { status.textContent = String(error?.stack ?? error); console.error(error); }
