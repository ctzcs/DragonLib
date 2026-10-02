import { dotnet } from './_framework/dotnet.js';
import { createFosterBackend } from './foster.js';

const status = document.getElementById('status');
function error(e) { console.error(e); status.textContent = String(e?.stack ?? e); status.dataset.state = 'error'; }
try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    const backend = createFosterBackend(document.getElementById('canvas'), error);
    runtime.setModuleImports('foster-web', backend.imports);
    const framework = await runtime.getAssemblyExports('Foster.Framework.dll');
    backend.attach(framework.Foster.Framework.WebRuntime);
    // Assets are fetched before Startup so Foster's synchronous title-storage API works.
    const manifestResponse = await fetch('./assets.json');
    if (!manifestResponse.ok) throw new Error(`Asset manifest failed: ${manifestResponse.status}`);
    const manifest = await manifestResponse.json();
    await Promise.all(manifest.map(async asset => {
        const response = await fetch(asset.url);
        if (!response.ok) throw new Error(`Asset ${asset.path} failed: ${response.status}`);
        framework.Foster.Framework.WebRuntime.AddAsset(asset.path, new Uint8Array(await response.arrayBuffer()));
    }));
    await runtime.runMain();
    status.textContent = 'Running — click the canvas to focus keyboard input.';
    status.dataset.state = 'running';
    const app = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    if (app.WebDemo?.Status) setInterval(() => {
        if (status.dataset.state !== 'error') status.textContent = app.WebDemo.Status();
    }, 500);
} catch (e) { error(e); }
