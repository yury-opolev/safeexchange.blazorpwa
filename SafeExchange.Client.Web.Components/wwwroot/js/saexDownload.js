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
