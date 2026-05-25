// saexDownload.js
//
// Verified-download helper. Two back-ends:
//   1) File System Access API (Chromium/Safari/Edge) — streams bytes to user-chosen
//      file location, aborts cleanly on integrity failure.
//   2) In-memory Blob (Firefox / fallback) — accumulates bytes in memory, only
//      reveals via <a download> after verification succeeds.
//
// Surface exposed to C#:
//   window.saexDownload = {
//     startVerifiedSave(fileName, contentType) -> Promise<handleId>
//     writeBlock(handleId, uint8Array)         -> Promise<void>
//     abort(handleId)                           -> Promise<void>
//     finalize(handleId)                        -> Promise<void>  (reveals the save)
//   };

(function () {
    const handles = new Map();
    let nextId = 1;

    async function startVerifiedSave(fileName, contentType) {
        const id = String(nextId++);
        const suggestedName = fileName && fileName.length > 0 ? fileName : "download";
        if (typeof window.showSaveFilePicker === "function") {
            try {
                // Only pass `suggestedName` — the `types` option would constrain
                // the save-as file-type filter and has been observed to cause the
                // picker to ignore the suggested name on some Chromium builds,
                // surfacing a browser-generated name (looks GUID-ish) instead.
                const fileHandle = await window.showSaveFilePicker({ suggestedName });
                const writable = await fileHandle.createWritable();
                handles.set(id, { kind: "fsa", writable, fileHandle, fileName: suggestedName, contentType });
                return id;
            } catch (err) {
                // Any FSA failure (including AbortError from user cancellation or
                // test-runner interception) falls through to the in-memory blob path.
                // The blob path verifies bytes in memory and only reveals the download
                // via <a download> after a hash match, which preserves the safety invariant
                // even if the user bailed out of the save picker.
            }
        }
        handles.set(id, { kind: "blob", buffers: [], fileName: suggestedName, contentType });
        return id;
    }

    async function writeBlock(id, bytes) {
        const h = handles.get(id);
        if (!h) {
            throw new Error("Unknown download handle.");
        }
        if (h.kind === "fsa") {
            await h.writable.write(bytes);
        } else {
            // Copy — the underlying buffer may be reused by the Blazor interop layer.
            h.buffers.push(bytes.slice());
        }
    }

    async function abort(id) {
        const h = handles.get(id);
        if (!h) {
            return;
        }
        if (h.kind === "fsa") {
            try { await h.writable.abort(); } catch { /* swallow */ }
        }
        handles.delete(id);
    }

    async function finalize(id) {
        const h = handles.get(id);
        if (!h) {
            throw new Error("Unknown download handle.");
        }
        if (h.kind === "fsa") {
            try { await h.writable.close(); } finally { handles.delete(id); }
        } else {
            const blob = new Blob(h.buffers, { type: h.contentType || "application/octet-stream" });
            const url = URL.createObjectURL(blob);
            try {
                const a = document.createElement("a");
                a.href = url;
                a.download = h.fileName;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
            } finally {
                URL.revokeObjectURL(url);
                handles.delete(id);
            }
        }
    }

    // Programmatically click a hidden <input type="file"> by element id.
    // Used by the "Verify local file…" kebab action — Blazor's <InputFile>
    // renders an <input>, and we want to open its native file-picker from
    // a C# event handler without going through `eval`, which the page's
    // CSP (script-src 'self' 'wasm-unsafe-eval') rightly blocks.
    function clickFileInput(id) {
        const el = document.getElementById(id);
        if (el) {
            el.click();
        }
    }

    window.saexDownload = { startVerifiedSave, writeBlock, abort, finalize, clickFileInput };
})();

// saexImages — render inline images via blob: object URLs instead of data: URIs.
//
// A multi-MB image as a `data:` URI is a huge base64 string copied through the
// WASM heap + DOM; iOS Safari/WKWebView refuses to paint large/dynamically-injected
// ones (blank-with-border). A blob: URL is a tiny handle to one native binary copy
// the browser decodes lazily — no size/memory wall. CSP already allows blob: in img-src.
//
// Lifecycle: createObjectUrl pins the bytes until revokeObjectUrl is called (or the
// document unloads). ViewData revokes on re-resolve and on dispose.
(function () {
    // base64 (string) -> blob: URL. We pass base64 rather than a byte[] because the
    // .NET->JS interop serializes byte[] as base64 anyway; decoding here avoids any
    // ambiguity and keeps a single tiny handle in the DOM.
    function createObjectUrl(base64, contentType) {
        const binary = atob(base64);
        const len = binary.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const blob = contentType ? new Blob([bytes], { type: contentType }) : new Blob([bytes]);
        return URL.createObjectURL(blob);
    }

    function revokeObjectUrl(url) {
        if (url && url.indexOf("blob:") === 0) {
            URL.revokeObjectURL(url);
        }
    }

    window.saexImages = { createObjectUrl, revokeObjectUrl };
})();
