// Foster C# WebGL2 backend. The .NET runtime registers this module as "foster-web".
// Coordinates: window drawing uses top-left pixels; texture row zero is logical top.
export function createFosterBackend(canvas, reportError = console.error) {
    let gl, runtime, active = false, frame = 0, previous = 0, observer, abort;
    let resizable = true, textInput = false, desiredFullscreen = false, desiredRelative = false;
    let mouseX = 0, mouseY = 0, clipboard = "", cursor = "default", mouseVisible = true;
    const events = [], heldKeys = new Set(), heldButtons = new Set(), pads = new Map();
    const objects = new Map(), programs = new Map(), samplers = new Map();
    let nextHandle = 1, white, vao, currentCommand, currentProgram;
    let colorBufferFloat = false; // DragonLib 扩展
    const retain = value => { const id = nextHandle++; objects.set(id, value); return id; };
    const get = (id, kind) => {
        const value = objects.get(id);
        if (!value || (kind && value.kind !== kind)) throw new Error(`Invalid ${kind ?? "resource"} handle ${id}`);
        return value;
    };
    const emit = (type, data = {}) => events.push({ type, ...data });
    const keys = {
        Enter: 40, Escape: 41, Backspace: 42, Tab: 43, Space: 44, Minus: 45, Equal: 46,
        BracketLeft: 47, BracketRight: 48, Backslash: 49, Semicolon: 51, Quote: 52,
        Backquote: 53, Comma: 54, Period: 55, Slash: 56, CapsLock: 57,
        PrintScreen: 70, ScrollLock: 71, Pause: 72, Insert: 73, Home: 74, PageUp: 75,
        Delete: 76, End: 77, PageDown: 78, ArrowRight: 79, ArrowLeft: 80, ArrowDown: 81, ArrowUp: 82,
        NumLock: 83, NumpadDivide: 84, NumpadMultiply: 85, NumpadSubtract: 86, NumpadAdd: 87,
        NumpadEnter: 88, Numpad0: 98, NumpadDecimal: 99, ContextMenu: 101,
        ControlLeft: 224, ShiftLeft: 225, AltLeft: 226, MetaLeft: 227,
        ControlRight: 228, ShiftRight: 229, AltRight: 230, MetaRight: 231
    };
    for (let i = 0; i < 26; i++) keys[`Key${String.fromCharCode(65 + i)}`] = 4 + i;
    for (let i = 1; i <= 9; i++) { keys[`Digit${i}`] = 29 + i; keys[`Numpad${i}`] = 88 + i; }
    keys.Digit0 = 39;
    for (let i = 1; i <= 12; i++) keys[`F${i}`] = 57 + i;
    for (let i = 13; i <= 24; i++) keys[`F${i}`] = 104 + i - 13;

    function resize() {
        const rect = canvas.getBoundingClientRect(), dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, Math.round(rect.width * dpr)), height = Math.max(1, Math.round(rect.height * dpr));
        if (canvas.width !== width || canvas.height !== height) {
            canvas.width = width; canvas.height = height; emit("resize");
        }
    }
    function releaseInput() {
        for (const key of heldKeys) emit("key", { key, down: false });
        for (const button of heldButtons) emit("button", { button, down: false });
        heldKeys.clear(); heldButtons.clear();
    }
    function position(e) {
        const r = canvas.getBoundingClientRect();
        const x = (e.clientX - r.left) * canvas.width / r.width;
        const y = (e.clientY - r.top) * canvas.height / r.height;
        const locked = document.pointerLockElement === canvas;
        const dx = locked ? e.movementX * canvas.width / r.width : x - mouseX;
        const dy = locked ? e.movementY * canvas.height / r.height : y - mouseY;
        mouseX = locked ? mouseX + dx : x; mouseY = locked ? mouseY + dy : y;
        emit("move", { x: mouseX, y: mouseY, dx, dy });
    }
    function gesture() {
        if (desiredFullscreen && document.fullscreenElement !== canvas)
            canvas.requestFullscreen().catch(reportError);
        if (desiredRelative && document.pointerLockElement !== canvas) {
            const result = canvas.requestPointerLock(); result?.catch?.(reportError);
        }
    }
    function focused() { return document.activeElement === canvas || document.pointerLockElement === canvas; }
    function on(target, type, handler, options = {}) { target.addEventListener(type, handler, { ...options, signal: abort.signal }); }
    function init(title, width, height, canResize, antialias) {
        if (gl) throw new Error("The Foster canvas is already initialized.");
        document.title = title;
        canvas.tabIndex = 0;
        canvas.style.width = `${width}px`; canvas.style.height = `${height}px`;
        canvas.style.maxWidth = canResize ? "100%" : "none"; canvas.style.touchAction = "none";
        resizable = canResize;
        gl = canvas.getContext("webgl2", { alpha: false, antialias, depth: true, stencil: true, preserveDrawingBuffer: false });
        if (!gl) throw new Error("WebGL2 is required to run Foster.Web.");
        colorBufferFloat = !!gl.getExtension("EXT_color_buffer_float");
        abort = new AbortController();
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1); gl.pixelStorei(gl.PACK_ALIGNMENT, 1);
        vao = gl.createVertexArray(); gl.bindVertexArray(vao);
        white = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, white);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([255, 255, 255, 255]));
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        resize();
        observer = new ResizeObserver(resize); observer.observe(canvas);
        on(window, "resize", resize);
        on(canvas, "focus", () => emit("focus"));
        on(canvas, "blur", () => { releaseInput(); emit("blur"); });
        on(window, "blur", releaseInput);
        on(canvas, "pointerenter", () => emit("enter"));
        on(canvas, "pointerleave", () => emit("leave"));
        on(canvas, "pointerdown", e => {
            canvas.focus({ preventScroll: true }); gesture(); position(e);
            const button = e.button === 0 ? 1 : e.button === 1 ? 2 : e.button === 2 ? 3 : e.button + 1;
            heldButtons.add(button); emit("button", { button, down: true });
            canvas.setPointerCapture(e.pointerId); e.preventDefault();
        });
        on(canvas, "pointerup", e => {
            position(e);
            const button = e.button === 0 ? 1 : e.button === 1 ? 2 : e.button === 2 ? 3 : e.button + 1;
            heldButtons.delete(button); emit("button", { button, down: false });
        });
        on(canvas, "pointercancel", releaseInput);
        on(canvas, "pointermove", position);
        on(canvas, "contextmenu", e => e.preventDefault());
        on(canvas, "wheel", e => {
            e.preventDefault(); const scale = e.deltaMode === 1 ? 1 : e.deltaMode === 2 ? 10 : .01;
            emit("wheel", { x: e.deltaX * scale, y: -e.deltaY * scale });
        }, { passive: false });
        on(window, "keydown", e => {
            if (!focused()) return;
            gesture(); const key = keys[e.code];
            if (key !== undefined && !e.repeat) { heldKeys.add(key); emit("key", { key, down: true }); }
            if (textInput && e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.isComposing) emit("text", { text: e.key });
            if (!e.ctrlKey && !e.metaKey && key !== undefined) e.preventDefault();
        });
        on(window, "keyup", e => {
            const key = keys[e.code]; if (heldKeys.delete(key)) emit("key", { key, down: false });
        });
        on(canvas, "compositionend", e => { if (textInput && e.data) emit("text", { text: e.data }); });
        on(document, "visibilitychange", () => {
            previous = 0; if (document.hidden) releaseInput();
            emit(document.hidden ? "background" : "foreground");
        });
        on(document, "fullscreenchange", () => { emit("fullscreen"); resize(); });
        on(window, "pagehide", () => { runtime?.Stop(); active = false; cancelAnimationFrame(frame); });
        on(canvas, "webglcontextlost", e => {
            e.preventDefault(); active = false; cancelAnimationFrame(frame); runtime?.Stop();
            reportError(new Error("WebGL context lost. Reload the page to restart the game."));
        });
    }
    function windowGet(p) {
        switch (p) {
            case "width": return canvas.getBoundingClientRect().width;
            case "height": return canvas.getBoundingClientRect().height;
            case "pixelWidth": return canvas.width;
            case "pixelHeight": return canvas.height;
            case "displayWidth": return screen.width;
            case "displayHeight": return screen.height;
            case "mouseX": return mouseX;
            case "mouseY": return mouseY;
            case "focused": return focused() && !document.hidden ? 1 : 0;
            case "fullscreen": return document.fullscreenElement === canvas ? 1 : 0;
            case "resizable": return resizable ? 1 : 0;
            default: throw new Error(`Unknown window property ${p}`);
        }
    }
    function windowSet(p, value) {
        switch (p) {
            case "title": document.title = value; break;
            case "size": {
                const [w, h] = value.split(",").map(Number);
                if (!(w > 0 && h > 0)) throw new Error("Invalid canvas dimensions");
                canvas.style.width = `${w}px`; canvas.style.height = `${h}px`; resize(); break;
            }
            case "resizable": resizable = value === "1"; canvas.style.maxWidth = resizable ? "100%" : "none"; resize(); break;
            case "fullscreen": desiredFullscreen = value === "1"; if (!desiredFullscreen && document.fullscreenElement === canvas) document.exitFullscreen().catch(reportError); break;
            case "relative": desiredRelative = value === "1"; if (!desiredRelative) document.exitPointerLock(); break;
            case "textInput": textInput = value === "1"; break;
            case "focus": canvas.focus({ preventScroll: true }); break;
            case "visible": canvas.style.visibility = value === "1" ? "visible" : "hidden"; break;
            case "cursor": cursor = value; canvas.style.cursor = mouseVisible ? cursor : "none"; break;
            case "mouseVisible": mouseVisible = value === "1"; canvas.style.cursor = mouseVisible ? cursor : "none"; break;
            default: throw new Error(`Unknown window property ${p}`);
        }
    }
    function pollGamepads() {
        const present = new Set();
        for (const pad of navigator.getGamepads?.() ?? []) {
            if (!pad || pad.mapping !== "standard") continue;
            const id = pad.index + 1; present.add(id);
            if (!pads.has(id)) { pads.set(id, { buttons: Array(15).fill(false), axes: Array(6).fill(0) }); emit("connect", { id, name: pad.id }); }
            const state = pads.get(id);
            const map = [0, 1, 2, 3, 8, 16, 9, 10, 11, 4, 5, 12, 13, 14, 15];
            map.forEach((dom, button) => {
                const down = pad.buttons[dom]?.pressed ?? false;
                if (down !== state.buttons[button]) { state.buttons[button] = down; emit("padButton", { id, button, down }); }
            });
            for (let axis = 0; axis < 6; axis++) {
                const value = axis < 4 ? pad.axes[axis] ?? 0 : pad.buttons[axis === 4 ? 6 : 7]?.value ?? 0;
                if (value !== state.axes[axis]) { state.axes[axis] = value; emit("padAxis", { id, axis, value }); }
            }
        }
        for (const id of pads.keys()) if (!present.has(id)) { pads.delete(id); emit("disconnect", { id }); }
    }
    function startLoop() {
        if (!runtime) throw new Error("Attach the Foster .NET exports before running the App.");
        if (active) return;
        active = true; previous = 0;
        function tick(now) {
            if (!active) return;
            const seconds = previous === 0 ? 0 : Math.min(.25, (now - previous) / 1000); previous = now;
            try { resize(); active = runtime.Step(seconds); }
            catch (error) { active = false; reportError(error); }
            if (active) frame = requestAnimationFrame(tick);
        }
        frame = requestAnimationFrame(tick);
    }
    function target(id) {
        const t = id ? get(id, "target") : null;
        gl.bindFramebuffer(gl.FRAMEBUFFER, t?.object ?? null);
        if (t) gl.drawBuffers(t.colors.map((_, i) => gl.COLOR_ATTACHMENT0 + i));
        else gl.drawBuffers([gl.BACK]);
        return t;
    }
    function checkFramebuffer() {
        if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) throw new Error("Incomplete WebGL framebuffer.");
    }
    // DragonLib 扩展：RGBA16F 采样虽为 core，Foster 的能力查询也承诺颜色附件可用。
    function textureFormatSupported(format) {
        return !!gl && ([0, 1, 2, 3, 5, 6, 7, 8].includes(format) || format === 9 && colorBufferFloat);
    }
    function create(kind, descriptor) {
        const d = JSON.parse(descriptor);
        if (kind === "target") return retain({ kind, object: gl.createFramebuffer(), width: d.width, height: d.height, colors: [], attachments: [] });
        if (kind === "buffer") return retain({ kind, object: gl.createBuffer(), target: d.type === 1 ? gl.ELEMENT_ARRAY_BUFFER : gl.ARRAY_BUFFER, size: 0 });
        if (kind === "shader") {
            const shader = gl.createShader(d.stage === 0 ? gl.VERTEX_SHADER : gl.FRAGMENT_SHADER);
            gl.shaderSource(shader, d.code.replace(/^\uFEFF/, "")); gl.compileShader(shader);
            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
                const error = gl.getShaderInfoLog(shader); gl.deleteShader(shader); throw new Error(`GLSL compile failed: ${error}`);
            }
            return retain({ kind, object: shader });
        }
        if (kind === "texture") {
            const formats = [
                [gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, 4], [gl.R8, gl.RED, gl.UNSIGNED_BYTE, 1], [gl.RG8, gl.RG, gl.UNSIGNED_BYTE, 2],
                [gl.DEPTH24_STENCIL8, gl.DEPTH_STENCIL, gl.UNSIGNED_INT_24_8, 4], null,
                [gl.DEPTH_COMPONENT16, gl.DEPTH_COMPONENT, gl.UNSIGNED_SHORT, 2],
                [gl.DEPTH_COMPONENT24, gl.DEPTH_COMPONENT, gl.UNSIGNED_INT, 4], [gl.DEPTH_COMPONENT32F, gl.DEPTH_COMPONENT, gl.FLOAT, 4],
                [gl.SRGB8_ALPHA8, gl.RGBA, gl.UNSIGNED_BYTE, 4], [gl.RGBA16F, gl.RGBA, gl.HALF_FLOAT, 8]
            ];
            const format = formats[d.format]; if (!format) throw new Error("Unsupported texture format");
            const object = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, object);
            gl.texStorage2D(gl.TEXTURE_2D, d.mipLevels, format[0], d.width, d.height);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
            const isDepth = d.format >= 3 && d.format <= 7;
            const id = retain({ kind, object, ...d, format, depth: isDepth });
            if (d.target) {
                const t = target(d.target), depth = isDepth;
                if (t.attachments.some(handle => get(handle).depth && depth)) throw new Error("Only one depth attachment is allowed");
                const point = depth ? d.format === 3 ? gl.DEPTH_STENCIL_ATTACHMENT : gl.DEPTH_ATTACHMENT : gl.COLOR_ATTACHMENT0 + t.colors.length;
                gl.framebufferTexture2D(gl.FRAMEBUFFER, point, gl.TEXTURE_2D, object, 0);
                t.attachments.push(id); if (!depth) t.colors.push(id);
                gl.drawBuffers(t.colors.map((_, i) => gl.COLOR_ATTACHMENT0 + i));
            }
            return id;
        }
        throw new Error(`Unknown resource type ${kind}`);
    }
    function destroy(id) {
        const value = objects.get(id); if (!value) return;
        if (value.kind === "shader") {
            for (const [key, p] of programs) if (p.shaders.includes(id)) {
                p.buffers.forEach(buffer => gl.deleteBuffer(buffer)); gl.deleteProgram(p.object); programs.delete(key);
            }
            gl.deleteShader(value.object);
        } else if (value.kind === "buffer") gl.deleteBuffer(value.object);
        else if (value.kind === "texture") gl.deleteTexture(value.object);
        else if (value.kind === "target") { for (const attachment of value.attachments) destroy(attachment); gl.deleteFramebuffer(value.object); }
        objects.delete(id);
    }
    function upload(id, view, offset, region) {
        const r = get(id), bytes = view.slice();
        if (r.kind === "buffer") {
            gl.bindBuffer(r.target, r.object);
            if (offset + bytes.length > r.size) {
                const oldSize = r.size, old = oldSize ? new Uint8Array(oldSize) : null;
                if (old) gl.getBufferSubData(r.target, 0, old);
                r.size = Math.max(offset + bytes.length, oldSize * 2, 256);
                gl.bufferData(r.target, r.size, gl.DYNAMIC_DRAW);
                if (old) gl.bufferSubData(r.target, 0, old);
            }
            gl.bufferSubData(r.target, offset, bytes);
        } else if (r.kind === "texture") {
            if (r.depth) throw new Error("Depth texture uploads are not supported");
            const [x, y, w, h] = JSON.parse(region);
            gl.bindTexture(gl.TEXTURE_2D, r.object);
            const data = r.format[2] === gl.HALF_FLOAT ? new Uint16Array(bytes.buffer, bytes.byteOffset, bytes.byteLength / 2) : bytes;
            gl.texSubImage2D(gl.TEXTURE_2D, 0, x, y, w, h, r.format[1], r.format[2], data);
            if (r.mipLevels > 1) gl.generateMipmap(gl.TEXTURE_2D);
        } else throw new Error("Resource cannot receive uploads");
    }
    function temporaryFramebuffer(texture) {
        if (texture.depth) throw new Error("Color textures are required for readback and blit");
        const fbo = gl.createFramebuffer(); gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture.object, 0);
        gl.drawBuffers([gl.COLOR_ATTACHMENT0]); checkFramebuffer(); return fbo;
    }
    function readTexture(id, view, region) {
        const r = get(id, "texture"), [x, y, w, h] = JSON.parse(region);
        const fbo = temporaryFramebuffer(r);
        try {
            // DragonLib 扩展：RGBA16F readPixels 只能读 FLOAT，再还原半浮点字节。
            if (r.format[2] === gl.HALF_FLOAT) {
                const floats = new Float32Array(w * h * 4);
                gl.readPixels(x, y, w, h, gl.RGBA, gl.FLOAT, floats);
                const halves = new Uint16Array(floats.length);
                const bits = new Uint32Array(floats.buffer);
                for (let i = 0; i < halves.length; i++) {
                    const sign = (bits[i] >>> 16) & 0x8000, exp = ((bits[i] >>> 23) & 255) - 127 + 15, mant = bits[i] & 0x7fffff;
                    halves[i] = exp >= 31 ? sign | 0x7c00 : exp <= 0 ? exp < -10 ? sign : sign | ((mant | 0x800000) >>> (14 - exp)) : sign | (exp << 10) | (mant >>> 13);
                }
                view.set(new Uint8Array(halves.buffer)); return;
            }
            // WebGL color readback is RGBA8. Extract R/RG channels when necessary.
            const rgba = new Uint8Array(w * h * 4); gl.readPixels(x, y, w, h, gl.RGBA, gl.UNSIGNED_BYTE, rgba);
            const result = new Uint8Array(w * h * r.format[3]);
            for (let i = 0; i < w * h; i++) for (let c = 0; c < r.format[3]; c++) result[i * r.format[3] + c] = rgba[i * 4 + c];
            view.set(result);
        } finally { gl.bindFramebuffer(gl.FRAMEBUFFER, null); gl.deleteFramebuffer(fbo); }
    }
    function blit(source, sourceRegion, dest, destRegion, filter) {
        const s = get(source, "texture"), d = get(dest, "texture");
        if (source === dest) throw new Error("Cannot blit a texture onto itself");
        const sf = temporaryFramebuffer(s), df = temporaryFramebuffer(d);
        const [sx, sy, sw, sh] = JSON.parse(sourceRegion), [dx, dy, dw, dh] = JSON.parse(destRegion);
        try {
            gl.disable(gl.SCISSOR_TEST); gl.bindFramebuffer(gl.READ_FRAMEBUFFER, sf); gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, df);
            gl.blitFramebuffer(sx, sy, sx + sw, sy + sh, dx, dy, dx + dw, dy + dh, gl.COLOR_BUFFER_BIT, filter === 0 ? gl.NEAREST : gl.LINEAR);
        } finally { gl.bindFramebuffer(gl.FRAMEBUFFER, null); gl.deleteFramebuffer(sf); gl.deleteFramebuffer(df); }
    }
    function program(vs, fs) {
        const key = `${vs}:${fs}`;
        if (programs.has(key)) return programs.get(key);
        const object = gl.createProgram(); gl.attachShader(object, get(vs, "shader").object); gl.attachShader(object, get(fs, "shader").object); gl.linkProgram(object);
        if (!gl.getProgramParameter(object, gl.LINK_STATUS)) {
            const error = gl.getProgramInfoLog(object); gl.deleteProgram(object); throw new Error(`GLSL link failed: ${error}`);
        }
        const p = { object, shaders: [vs, fs], buffers: [], blocks: new Map() };
        for (let stage = 0; stage < 2; stage++) for (let slot = 0; slot < 8; slot++) {
            const index = gl.getUniformBlockIndex(object, `${stage === 0 ? "Vertex" : "Fragment"}Uniform${slot}`);
            if (index !== gl.INVALID_INDEX) {
                const binding = stage * 8 + slot, size = gl.getActiveUniformBlockParameter(object, index, gl.UNIFORM_BLOCK_DATA_SIZE);
                const buffer = gl.createBuffer(); p.buffers.push(buffer);
                gl.bindBuffer(gl.UNIFORM_BUFFER, buffer); gl.bufferData(gl.UNIFORM_BUFFER, size, gl.DYNAMIC_DRAW);
                gl.uniformBlockBinding(object, index, binding); p.blocks.set(binding, { buffer, size });
            }
        }
        programs.set(key, p); return p;
    }
    function sampler(d) {
        const key = `${d.filter},${d.wrapX},${d.wrapY},${d.mipmaps}`; if (samplers.has(key)) return samplers.get(key);
        const object = gl.createSampler(), wrap = [gl.REPEAT, gl.MIRRORED_REPEAT, gl.CLAMP_TO_EDGE];
        gl.samplerParameteri(object, gl.TEXTURE_MIN_FILTER, d.mipmaps ? d.filter === 0 ? gl.NEAREST_MIPMAP_NEAREST : gl.LINEAR_MIPMAP_LINEAR : d.filter === 0 ? gl.NEAREST : gl.LINEAR);
        gl.samplerParameteri(object, gl.TEXTURE_MAG_FILTER, d.filter === 0 ? gl.NEAREST : gl.LINEAR);
        gl.samplerParameteri(object, gl.TEXTURE_WRAP_S, wrap[d.wrapX]); gl.samplerParameteri(object, gl.TEXTURE_WRAP_T, wrap[d.wrapY]);
        samplers.set(key, object); return object;
    }
    const toggle = (cap, enabled) => enabled ? gl.enable(cap) : gl.disable(cap);
    const compare = value => [gl.ALWAYS, gl.NEVER, gl.LESS, gl.EQUAL, gl.LEQUAL, gl.GREATER, gl.NOTEQUAL, gl.GEQUAL][value];
    function beginDraw(json) {
        const c = currentCommand = JSON.parse(json), t = target(c.target);
        if (t) checkFramebuffer();
        const w = t?.width ?? canvas.width, h = t?.height ?? canvas.height;
        const rect = c.viewport ?? [0, 0, w, h];
        gl.viewport(rect[0], t ? rect[1] : h - rect[1] - rect[3], rect[2], rect[3]);
        toggle(gl.SCISSOR_TEST, c.scissor !== null);
        if (c.scissor) { const [x, y, sw, sh] = c.scissor; gl.scissor(x, t ? y : h - y - sh, sw, sh); }
        const factors = [gl.ZERO, gl.ONE, gl.SRC_COLOR, gl.ONE_MINUS_SRC_COLOR, gl.DST_COLOR, gl.ONE_MINUS_DST_COLOR, gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.DST_ALPHA, gl.ONE_MINUS_DST_ALPHA, gl.CONSTANT_COLOR, gl.ONE_MINUS_CONSTANT_COLOR, gl.SRC_ALPHA_SATURATE];
        const ops = [gl.FUNC_ADD, gl.FUNC_SUBTRACT, gl.FUNC_REVERSE_SUBTRACT, gl.MIN, gl.MAX], b = c.blend;
        gl.enable(gl.BLEND); gl.blendFuncSeparate(factors[b[0]], factors[b[1]], factors[b[3]], factors[b[4]]); gl.blendEquationSeparate(ops[b[2]], ops[b[5]]);
        gl.blendColor(...c.blendColor); gl.colorMask(!!(b[6] & 1), !!(b[6] & 2), !!(b[6] & 4), !!(b[6] & 8));
        toggle(gl.CULL_FACE, c.cull !== 0); gl.cullFace(c.cull === 1 ? gl.FRONT : gl.BACK); gl.frontFace(t ? gl.CW : gl.CCW);
        toggle(gl.DEPTH_TEST, c.DepthTestEnabled); gl.depthMask(c.DepthWriteEnabled); gl.depthFunc(compare(c.depthCompare));
        toggle(gl.STENCIL_TEST, c.StencilTestEnabled); gl.stencilMask(c.StencilWriteMask);
        const stencilOps = [gl.KEEP, gl.KEEP, gl.ZERO, gl.REPLACE, gl.INCR, gl.DECR, gl.INVERT, gl.INCR_WRAP, gl.DECR_WRAP];
        if (c.StencilTestEnabled) for (const [face, s] of [[gl.FRONT, c.frontStencil], [gl.BACK, c.backStencil]]) {
            gl.stencilFuncSeparate(face, compare(s[3]), c.StencilReferenceValue, c.StencilCompareMask);
            gl.stencilOpSeparate(face, stencilOps[s[0]], stencilOps[s[1]], stencilOps[s[2]]);
        }
        currentProgram = program(c.vertexShader, c.fragmentShader); gl.useProgram(currentProgram.object);
        gl.uniform1f(gl.getUniformLocation(currentProgram.object, "u_target_flip"), t ? -1 : 1);
        // Clear all declared blocks so omitted uniforms never inherit a previous draw's data.
        for (const [binding, block] of currentProgram.blocks) {
            gl.bindBuffer(gl.UNIFORM_BUFFER, block.buffer); gl.bufferSubData(gl.UNIFORM_BUFFER, 0, new Uint8Array(block.size));
            gl.bindBufferBase(gl.UNIFORM_BUFFER, binding, block.buffer);
        }
        const maxAttributes = gl.getParameter(gl.MAX_VERTEX_ATTRIBS);
        gl.bindVertexArray(vao);
        for (let i = 0; i < maxAttributes; i++) { gl.disableVertexAttribArray(i); gl.vertexAttribDivisor(i, 0); }
        const types = [null, [1, gl.FLOAT], [2, gl.FLOAT], [3, gl.FLOAT], [4, gl.FLOAT], [4, gl.BYTE], [4, gl.UNSIGNED_BYTE], [2, gl.SHORT], [2, gl.UNSIGNED_SHORT], [4, gl.SHORT], [4, gl.UNSIGNED_SHORT]];
        for (const v of c.vertices) {
            gl.bindBuffer(gl.ARRAY_BUFFER, get(v.handle, "buffer").object);
            for (const a of v.attributes) {
                const [size, type] = types[a.type]; gl.enableVertexAttribArray(a.location);
                const offset = a.offset + (c.indexBuffer && !v.instance ? c.VertexOffset * v.stride : 0);
                gl.vertexAttribPointer(a.location, size, type, a.normalized, v.stride, offset);
                gl.vertexAttribDivisor(a.location, v.instance ? 1 : 0);
            }
        }
        let unit = 0;
        for (const [stage, list] of [["fragment", c.fragmentSamplers], ["vertex", c.vertexSamplers]]) list.forEach((s, slot) => {
            const texture = s.handle ? get(s.handle, "texture") : null;
            if (texture?.target === c.target && c.target !== 0) throw new Error("Cannot sample an attachment of the active render target");
            gl.activeTexture(gl.TEXTURE0 + unit); gl.bindTexture(gl.TEXTURE_2D, texture?.object ?? white); gl.bindSampler(unit, sampler(s));
            gl.uniform1i(gl.getUniformLocation(currentProgram.object, `u_${stage}_tex${slot}`), unit++);
        });
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, c.indexBuffer ? get(c.indexBuffer, "buffer").object : null);
    }
    function uniform(stage, slot, view) {
        const block = currentProgram.blocks.get(stage * 8 + slot); if (!block) return;
        const bytes = view.slice(); if (bytes.length > block.size) throw new Error("Uniform data exceeds the GLSL block size");
        gl.bindBuffer(gl.UNIFORM_BUFFER, block.buffer); gl.bufferSubData(gl.UNIFORM_BUFFER, 0, bytes);
    }
    function draw() {
        const c = currentCommand;
        if (c.InstanceCount <= 0) return;
        if (c.indexBuffer) gl.drawElementsInstanced(gl.TRIANGLES, c.IndexCount, c.indexFormat === 0 ? gl.UNSIGNED_SHORT : gl.UNSIGNED_INT, c.IndexOffset * (c.indexFormat === 0 ? 2 : 4), c.InstanceCount);
        else gl.drawArraysInstanced(gl.TRIANGLES, c.VertexOffset, c.VertexCount, c.InstanceCount);
    }
    function clear(id, json, depth, stencil, mask) {
        const t = target(id), colors = JSON.parse(json); if (t) checkFramebuffer();
        gl.disable(gl.SCISSOR_TEST); gl.colorMask(true, true, true, true); gl.depthMask(true); gl.stencilMask(255);
        if (mask & 1) {
            const count = t?.colors.length ?? 1;
            for (let i = 0; i < count; i++) gl.clearBufferfv(gl.COLOR, i, colors[i] ?? colors[0] ?? [0, 0, 0, 0]);
        }
        if (mask & 2) gl.clearBufferfv(gl.DEPTH, 0, [depth]);
        if (mask & 4) gl.clearBufferiv(gl.STENCIL, 0, [stencil]);
    }
    function dispose() {
        active = false; cancelAnimationFrame(frame); observer?.disconnect(); abort?.abort();
        if (gl) {
            for (const id of [...objects.keys()]) destroy(id);
            for (const s of samplers.values()) gl.deleteSampler(s); samplers.clear();
            gl.deleteTexture(white); gl.deleteVertexArray(vao);
        }
        if (document.pointerLockElement === canvas) document.exitPointerLock();
        gl = null; heldKeys.clear(); heldButtons.clear(); pads.clear(); events.length = 0;
    }
    return {
        attach(exports) { runtime = exports; },
        imports: {
            init, windowGet, windowSet, startLoop, dispose, create, destroy, upload, readTexture, blit, beginDraw, uniform, draw, clear, textureFormatSupported,
            pollEvents() { pollGamepads(); return JSON.stringify(events.splice(0)); },
            storageGet: key => localStorage.getItem(key),
            storageSet: (key, value) => value === null ? localStorage.removeItem(key) : localStorage.setItem(key, value),
            storageKeys: prefix => JSON.stringify(Object.keys(localStorage).filter(k => k.startsWith(prefix))),
            clipboardGet: () => clipboard,
            clipboardSet(value) { clipboard = value; navigator.clipboard?.writeText(value).catch(reportError); }
        }
    };
}
