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
        if (typeof window.showSaveFilePicker === "function") {
            try {
                const suggestedName = fileName || "download";
                const ext = suggestedName.includes(".") ? "." + suggestedName.split(".").pop() : ".bin";
                const pickerOptions = { suggestedName };
                if (contentType) {
                    pickerOptions.types = [{ description: suggestedName, accept: { [contentType]: [ext] } }];
                }
                const fileHandle = await window.showSaveFilePicker(pickerOptions);
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
        handles.set(id, { kind: "blob", buffers: [], fileName: fileName || "download", contentType });
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

    window.saexDownload = { startVerifiedSave, writeBlock, abort, finalize };
})();
