# Attachments Integrity — Design Spec

**Date:** 2026-04-19
**Feature branch:** `features/attachments-integrity` (client repo) + matching backend branch
**Related OWASP finding:** P1 #3 — *Chunked upload has no ordering/commit/integrity safeguards; `ChunkMetadata.Hash` ignored on read* (`docs/owasp/A06-insecure-design.md`)
**Scope:** Blazor WASM client (this repo) + Azure Functions backend (`C:\Users\yurio\Documents\github\safeexchange`)

---

## 1. Summary

End-to-end integrity for attachment uploads and downloads. Three layers that each defend against a different class of failure:

1. **Per-chunk SHA-256** verified at the server on every chunk upload.
2. **Whole-content SHA-256** computed by both client and server in parallel, verified at an explicit commit step.
3. **Whole-content verification** performed client-side on every download, against the hash stored with the attachment.

Plus:

- UI indication of each attachment's hash (truncated + hover tooltip + copy-to-clipboard, matching the `LoginDisplay` session-id pattern).
- Status badges on the attachment row: verified / legacy / failed.
- **Verify local file…** action on each attachment row — user picks a local file, client computes SHA-256 and compares against the stored hash. Purely local.

Scope boundary: hashing applies only to attachments (`ContentMetadata.IsMain == false`). The main HTML content keeps its current sanitised single-chunk path unchanged.

---

## 2. Goals / non-goals

**Goals:**

- Detect silent transport corruption (chunked-upload glitches, flaky proxy).
- Detect at-rest corruption / storage drift (bit-rot, blob misconfig, future migration drops).
- Detect active tamper if TLS fails (defense-in-depth).
- Detect "upload looks complete but a chunk was dropped mid-stream".
- Detect chunk reordering.
- Surface integrity failures to the user in a way they cannot miss.
- Make the feature zero-third-party — no new NuGet packages.

**Non-goals:**

- Resumable/checkpointed uploads (orthogonal feature).
- End-to-end encryption of attachment content (separate threat model; see `docs/owasp/A04-cryptographic-failures.md` P4 #1).
- Backfilling hashes for already-uploaded attachments (unattested hash would be worse than honest "legacy" badge).
- Verifying the integrity of the `IsMain` HTML content (already covered by the server-side sanitizer).

---

## 3. Threat model

The existing project assumption stands: **backend is trusted; confidentiality is TLS-bound.** Integrity, however, is not something we want to delegate entirely to TLS.

Threats the design must catch:

| # | Threat | Layer that catches it |
|---|---|---|
| A | Silent transport corruption between browser and backend | per-chunk hash at upload |
| B | At-rest storage corruption / bit-rot | whole-content hash at download |
| C | Active tamper if TLS is broken (MITM, rogue proxy) | per-chunk hash at upload |
| D | Upload "looks complete" but dropped bytes mid-stream | whole-content commit (client + server running hash) |
| E | Chunk reordering (two chunk bodies swapped) | whole-content commit + download-side verify |

Out of scope:

- Malicious uploader stamping fake content — infeasible (requires SHA-256 preimage); the reader would detect any mismatch.
- Malicious backend operator tampering with the stored hash — we don't defend against admin-level compromise.
- Side-channel leaks from hash values themselves — they're not secrets.

---

## 4. Architecture

### 4.1 Three integrity layers

```
Upload time                   Commit time                   Download time
─────────────                  ───────────                   ─────────────
per-chunk hash        ─→       whole-content hash    ─→      whole-content hash
(server verifies                (client asserts,               (client re-computes,
what it received vs.             server re-computes             compares to stored
what client claimed)             from persisted running         ContentMetadata.Hash)
                                 state and compares)
```

- **Client-side whole-content hash** uses .NET's `IncrementalHash` (one upload session = one HTTP client, so no state-serialisation needed).
- **Server-side whole-content hash** must survive across chunk-upload HTTP requests. .NET's `IncrementalHash` does not expose serialisable state, so we hand-roll a `SerializableSha256` class with explicit save/restore.
- **Per-chunk hashes** on both client and server use the built-in `IncrementalHash` — one request, no persistence.

### 4.2 Scope boundary

`ContentMetadata.IsMain == true` (main HTML body) → **unchanged**. Single-chunk upload, sanitised server-side, no hash, no UI badge.

`ContentMetadata.IsMain == false` (attachment) → full three-layer integrity.

---

## 5. Wire format

### 5.1 HTTP headers

| Header | Where | Value | Required? |
|---|---|---|---|
| `X-SafeExchange-Chunk-Hash` | `POST .../chunk` request | 64-char lowercase hex of SHA-256 of chunk body | Required for attachments (`IsMain == false`); ignored for main content. Absent ⇒ 400 for attachments (unless `AllowLegacyAttachmentUploads=true`, see §10). |
| `X-SafeExchange-Ticket` | `POST .../chunk` and `PATCH .../commit` | Access ticket from prior response | Same as today |
| `X-SafeExchange-OpType: interim` | `POST .../chunk` (non-final chunk) | `"interim"` | Same as today. Still used as a lifecycle hint, but no longer triggers `Ready`; see §6.2. |

### 5.2 New endpoint

```
PATCH /v2/secret/{secretId}/content/{contentId}/commit

Headers:
  X-SafeExchange-Ticket: <access-ticket>

Body:
  { "hash": "<64-char lowercase hex>" }

Responses:
  200 { "status": "ok",  "result": { "contentName": "...", "hash": "..." } }
  400 { "status": "bad_request", "error": "hash format invalid" }
  401 (missing/expired access ticket)
  403 (no write permission)
  404 (secret/content not found)
  422 { "status": "no_upload_state", "error": "commit called before any hashed chunk was accepted" }
  422 { "status": "hash_mismatch",
        "error": "client-asserted hash does not match server running hash",
        "result": { "expected": "<client-claim>", "actual": "<server-computed>" } }
```

### 5.3 DTO changes

**Server output DTOs** (`SafeExchange.Core/Model/Dto/Output`):

- `ChunkOutput.Hash` — already present, now populated (non-empty for new uploads, empty for legacy).
- `ChunkCreationOutput.Hash` — same.
- `ContentMetadataOutput.Hash` — **new field**, `string?`. `null` or empty = legacy.

**Client input/DTOs** (`SafeExchange.Client.Common/Model`):

- `ChunkMetadata.Hash` — already present, now actually consulted on read.
- `ContentMetadata.Hash` — **new property**, `string?`.

### 5.4 Database schema (backend EF migration)

Add to `ContentMetadata`:

```csharp
public string? Hash { get; set; }                   // nvarchar(64), nullable
public byte[]? RunningHashState { get; set; }       // varbinary(128), nullable, transient
```

- `Hash`: set at commit, cleared never. `null` for legacy rows.
- `RunningHashState`: populated during active hashed-mode upload, cleared at commit success. Never exposed over the wire.

Existing `ChunkMetadata.Hash` column reused as-is.

One migration: `AddAttachmentIntegrityColumns`. No data migration.

---

## 6. Upload flow

### 6.1 Client

`ApiClient.UploadAttachmentsAsync` per attachment:

1. `CreateContentMetadataAsync` → receive `contentId`.
2. `using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);`
3. Open `IBrowserFile.OpenReadStream(Constants.MaxAttachmentDataLength)`.
4. Chunk loop (index `i = 0 .. N-1`):
    a. Read up to `Constants.MaxChunkDataLength` bytes into a pooled buffer.
    b. `using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);`
    c. `chunkHasher.AppendData(buffer); fileHasher.AppendData(buffer);`
    d. `chunkHash = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();`
    e. `PutSecretDataStreamAsync(..., chunkHash, accessTicket)` with `isInterim = (i < N - 1)` — the new `chunkHash` argument populates `X-SafeExchange-Chunk-Hash`.
    f. On 422 `chunk_hash_mismatch`: retry the same chunk **once**. On second failure, mark attachment `UploadStatus.Error` with a user-visible error.
    g. On 200: capture `accessTicket` from the response.
5. `contentHash = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();`
6. `CommitContentAsync(secretId, contentId, contentHash, accessTicket)` (new method):
    - `PATCH /v2/secret/{secretId}/content/{contentId}/commit` with body `{ "hash": contentHash }`, header `X-SafeExchange-Ticket`.
    - On 200: mark `UploadStatus.Success`.
    - On 422 `hash_mismatch`: mark `UploadStatus.Error` with a loud error (something diverged — do not silently retry; user re-uploads).
    - On 422 `no_upload_state`: same (client bug: committed without hashed chunks). Fail loudly.

### 6.2 Server — chunk upload handler (revised `HandleSecretStreamUpload`)

Existing checks (secret, permission, `KeepInStorage`, content exists, `chunkId` empty, access-ticket match) are unchanged.

Pseudo-code for the integrity-relevant additions:

```csharp
var headerHash = request.Headers.TryGetValues("X-SafeExchange-Chunk-Hash", out var v) ? v.FirstOrDefault() : null;
var mode = InferMode(existingContent, headerHash, features.AllowLegacyAttachmentUploads);
// mode = HashedMode | LegacyMode | Reject

if (mode == Reject) return 400 "bad_request";

if (mode == HashedMode) {
    var running = existingContent.RunningHashState is null
        ? new SerializableSha256()
        : SerializableSha256.Restore(existingContent.RunningHashState);

    using var chunkHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    // Stream request.Body through a wrapper that forwards bytes to BOTH:
    //   - blobHelper.EncryptAndUploadBlobAsync (existing)
    //   - chunkHash.AppendData  (per-chunk)
    //   - running.Append        (whole-content running)
    // Implementation: a minimal DelegatingStream that Reads from request.Body and
    // forwards each span to the two hashers before returning bytes to the blob uploader.

    var uploaded = await blobHelper.EncryptAndUploadBlobAsync(newChunkName, teeStream);
    var computed = Convert.ToHexString(chunkHash.GetHashAndReset()).ToLowerInvariant();

    if (computed != headerHash) {
        await blobHelper.DeleteBlobAsync(newChunkName);
        return 422 { status: "chunk_hash_mismatch",
                     result: { expected: headerHash, actual: computed } };
    }

    existingContent.Chunks.Add(new ChunkMetadata { ChunkName = newChunkName,
                                                   Hash = computed, Length = uploaded });
    existingContent.RunningHashState = running.SaveState();
    // IMPORTANT: do NOT flip Ready even on the non-interim chunk — commit endpoint does that.
    await dbContext.SaveChangesAsync();

    return 200 ChunkCreationOutput { ChunkName, Hash = computed, Length = uploaded, AccessTicket };
}

if (mode == LegacyMode) {
    // preserve today's behaviour verbatim:
    //   - no hash verify
    //   - store Hash = ""
    //   - non-interim chunk flips ContentStatus.Ready
    //   - no RunningHashState ever populated
}
```

`InferMode` logic:

| flag on? | header present? | existing mode (`RunningHashState`) | resolved mode |
|---|---|---|---|
| any | yes | null (no prior chunks) | HashedMode |
| any | yes | non-null (prior hashed chunks) | HashedMode |
| any | yes | null (prior legacy chunks) | **Reject** (inconsistent) |
| `true` | no | null | LegacyMode |
| `true` | no | non-null | **Reject** (inconsistent) |
| `false` | no | null | **Reject** (header required when flag off) |

"Existing mode" is inferred from the combination of `RunningHashState` and `Chunks.Count`:

- `Chunks.Count == 0 && RunningHashState == null` → no prior chunks, no mode locked
- `RunningHashState != null` → hashed-mode locked
- `Chunks.Count > 0 && RunningHashState == null` → legacy-mode locked

### 6.3 Server — commit handler (new function `SafeExchangeContentCommit`)

```csharp
public async Task<HttpResponseData> Run(HttpRequestData request, string secretId,
                                        string contentId, ClaimsPrincipal principal, ILogger log)
```

Wire:

```
PATCH /v2/secret/{secretId}/content/{contentId}/commit
```

Steps:

1. Global filters, subject resolution, purger — same pattern as `SafeExchangeSecretStream`.
2. Load `existingMetadata` + `existingContent`; existence/permission/`KeepInStorage`/`IsMain == false` gates.
3. Parse body `{ "hash": "..." }`. Regex-validate 64-char lowercase hex.
4. Access-ticket header must match `existingContent.AccessTicket`.
5. If `existingContent.RunningHashState == null` ⇒ 422 `no_upload_state`.
6. `var serverHash = SerializableSha256.Restore(existingContent.RunningHashState).Finish();`
7. Compare to body hash.
    - Match:
        - `existingContent.Hash = bodyHash;`
        - `existingContent.Status = ContentStatus.Ready;`
        - `existingContent.AccessTicket = String.Empty;`
        - `existingContent.AccessTicketSetAt = DateTime.MinValue;`
        - `existingContent.RunningHashState = null;`
        - `SaveChangesAsync;` → 200.
    - Mismatch: 422 `hash_mismatch`; do not mutate state (client decides whether to reconcile or fail).

### 6.4 Failure / lifecycle interactions

- **Access-ticket timeout.** Existing `TryGetAccessTicketAsync` resets the content via the purger on expiry. The revision **must also clear `RunningHashState`** alongside chunks. Otherwise a subsequent upload would inherit stale running hash state.
- **Client gives up mid-upload.** Same as today — access-ticket timer reaps. No new code path.
- **Client retries a chunk.** Per-chunk mismatch returns 422 without mutating state, so the next attempt starts clean.
- **Client retries commit.** Idempotent: a second commit on an already-`Ready` content returns 200 if hashes match (or 422 otherwise). Simpler to just require `RunningHashState != null` as the precondition; second commit → `no_upload_state`. Acceptable.

---

## 7. Download flow

### 7.1 Client — verified-download path

Driven by an action on the attachment row (same trigger as today's `Download` button).

1. Read `ContentMetadataOutput` from the already-fetched secret payload.
2. **Legacy detection:** `string.IsNullOrEmpty(contentMetadata.Hash)` ⇒ set the legacy badge, skip verify, fall back to today's plain-download path. Stop here.
3. **Size-independent streaming path:**
    a. Call JS interop `saexDownload.startVerifiedSave(fileName, contentType)` → returns an opaque handle backed by a `FileSystemWritableFileStream` (picked via `showSaveFilePicker`). If the browser does not support FSA (Firefox), falls back to an in-memory Blob accumulator; returns a handle around it. This is documented but not optimised.
    b. `using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);`
    c. For each `ChunkMetadata` in `contentMetadata.Chunks` (iterated in stored order):
        i. `GetSecretDataStreamAsync(secretId, contentId, chunk.ChunkName)` → response body stream.
        ii. `using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);`
        iii. Read the body in block-sized buffers; for each block: `chunkHasher.AppendData(block); fileHasher.AppendData(block); await saexDownload.writeBlock(handle, block);`
        iv. After the body drains: `computed = chunkHasher.GetHashAndReset() hex`. Compare to `chunk.Hash`.
            - Match: continue.
            - Mismatch: `await saexDownload.abort(handle);` (aborts the writable stream; browser deletes the partial file on Chromium/Safari; truncated stub may remain on others). Emit `AttachmentVerifyFailed`. Show the failure modal (§8.4). Return.
    d. After all chunks: `computed = fileHasher.GetHashAndReset() hex`. Compare to `contentMetadata.Hash`.
        - Match: `await saexDownload.close(handle);` (finalises the save). Emit `AttachmentVerified`. Flip the attachment row's status badge to verified. Show success toast.
        - Mismatch: `await saexDownload.abort(handle);`. Same failure modal. (Rare: all chunks passed per-chunk, but the whole-content hash didn't — either a client-side bug or coordinated per-chunk tampering where the attacker also updated the chunk hashes in the DB.)

### 7.2 Firefox / unsupported-browser fallback

- One-time amber banner: `"Verified downloads on this browser keep the file in memory until verification completes."`
- Implementation: JS helper's `startVerifiedSave` on a browser without `showSaveFilePicker` returns a handle backed by an array of `ArrayBuffer`s. `writeBlock` pushes; `abort` discards; `close` concatenates into a `Blob`, `URL.createObjectURL`, triggers `<a download>`, revokes URL.
- Memory: O(file size). Acceptable compromise for a minority of users.

### 7.3 Local-file verify

On the attachment row's kebab menu:

1. "Verify local file…" click → hidden `<InputFile>` opens OS file picker.
2. Progress toast: `"Computing hash for <filename>…"`.
3. Stream the picked file in block-sized buffers through `IncrementalHash`. Same block size as the upload path to reuse buffer pooling.
4. Compare `computed` to `contentMetadata.Hash`.
5. Toast result:
    - Match (green): `"✓ <filename> matches this attachment."`
    - Mismatch (red): `"✗ <filename> does NOT match. Computed: <first 12 hex>… "` + copy button for the full value.
6. Auto-dismiss ~8 s unless hovered.
7. Telemetry: `AttachmentLocalVerified` with `attachmentId` + `result ∈ {match, mismatch}`. No file name. No hash value.
8. Disabled / greyed for legacy attachments (no reference hash).

---

## 8. UI

### 8.1 Attachment row layout

```
📎  report.pdf              4.2 MB    a1b2c3d4…  [📋]   [badge]   [⬇ Download]  [⋯]
```

- Filename, size — unchanged.
- **Hash cell:** truncated hex (first 8 chars + `…`), Bootstrap tooltip on hover shows full 64 hex, adjacent `bi-clipboard` button copies full hex. Pattern matches `LoginDisplay.razor`'s Session row. Empty for legacy attachments.
- **Status badge:** see §8.2.
- Download button — unchanged trigger, new underlying flow (§7.1).
- **Kebab menu** now includes `Verify local file…` (disabled for legacy).

### 8.2 Status badge states

| State | Icon | Colour | Tooltip |
|---|---|---|---|
| Verified | `bi-shield-fill-check` | green | `"Integrity verified on download."` |
| Not yet downloaded this session | _(no badge)_ | — | — |
| Legacy | `bi-question-diamond` | grey | `"Uploaded before the integrity feature — no reference hash is stored. Re-upload this attachment to enable verification."` |
| Failed | `bi-exclamation-diamond` | red | `"Last download failed the integrity check. The file on disk may be partial or corrupt."` |

Badge state is session-scoped (reset on page reload / sign out / sign in).

### 8.3 Download progress toast

- Non-modal, appears at the top-right when a verified download starts.
- Content: `"Downloading + verifying <filename>…"` with a progress bar (bytes-in / total).
- On success: morphs to `"✓ <filename> verified and saved."` and auto-dismisses after 3 s.
- On failure: dismissed silently, the failure modal takes over.

### 8.4 Download failure modal

Modal (not a toast — failure is security-relevant):

- Header (red): `"Integrity check failed"`
- Body: `"The download of <filename> could not be verified against the reference hash stored with this attachment. The file at <path> may be partial or corrupt — please delete it."`
- `[Copy debug info]` — copies `attachmentId`, `failedAt` (`chunk:<n>` | `wholeContent`), expected hash (first 12 chars), computed hash (first 12 chars) to clipboard. For support.
- `[Dismiss]`

Persistent red `bi-exclamation-diamond` badge on the row until a later successful download.

### 8.5 Telemetry events

Emitted via the existing `TelemetryService`. Custom dimensions listed; **no file names, no hash values, no PII**.

| Event | Dimensions |
|---|---|
| `AttachmentUploadStarted` | `attachmentId`, `mode` (`hashed`/`legacy`), `sizeBucket` |
| `AttachmentUploadSucceeded` | `attachmentId`, `mode`, `sizeBucket`, `chunkCount` |
| `AttachmentUploadFailed` | `attachmentId`, `mode`, `failedAt` (`chunk:<n>`/`commit`/`createMetadata`) |
| `AttachmentVerified` | `attachmentId`, `sizeBucket` |
| `AttachmentVerifyFailed` | `attachmentId`, `failedAt` (`chunk:<n>`/`wholeContent`), `sizeBucket` |
| `AttachmentLocalVerified` | `attachmentId`, `result` (`match`/`mismatch`) |

`sizeBucket` discretises file size to avoid fingerprinting: `<1M`, `1-10M`, `10-100M`, `>100M`.

---

## 9. Hand-rolled SHA-256 (`SerializableSha256`)

**Located:** `SafeExchange.Core/Crypto/SerializableSha256.cs` (backend-only; client uses built-in `IncrementalHash`).

**Public API:**

```csharp
public sealed class SerializableSha256
{
    public SerializableSha256();                             // new, empty state
    public void Append(ReadOnlySpan<byte> data);
    public byte[] Finish();                                  // 32 bytes
    public byte[] SaveState();                               // ~96 bytes, deterministic
    public static SerializableSha256 Restore(byte[] state);  // from SaveState output
}
```

**State layout** (stable across versions — document the binary format):

- 1 byte: version marker (`0x01`).
- 32 bytes: H0..H7 as 8 uint32 big-endian.
- 1 byte: partial-block length (0..63).
- 64 bytes: partial-block buffer (padded with zeros if partial).
- 8 bytes: total bit count as uint64 big-endian.
- **Total: 106 bytes.** Fits `varbinary(128)` with headroom.

**Implementation constraints:**

- Pure managed code, no `unsafe`.
- No allocations in the hot path (append): work on `Span<byte>` and pre-sized buffers.
- Strict FIPS 180-4 conformance — no "shortcuts" that might drift from the spec.

---

## 10. Backward compatibility & rollout

### 10.1 Backend flags

Two new flags, both under the existing `Features` configuration section (bound from Key Vault / App Configuration — whichever the existing `Features` bindings use; confirm during implementation):

- **`AllowLegacyAttachmentUploads`** (bool, default `true` on first release) — rollout flag.
- **`IgnoreChunkHashHeader`** (bool, default `false`) — emergency rollback flag; details in §10.3.

When `AllowLegacyAttachmentUploads == true`:

- Chunk upload handler accepts requests without `X-SafeExchange-Chunk-Hash` → legacy mode (existing behaviour preserved verbatim).
- With header present → hashed mode.

When `AllowLegacyAttachmentUploads == false`:

- Any attachment chunk upload without `X-SafeExchange-Chunk-Hash` → 400.
- Main-content uploads unaffected.

Mode-lock per content applies in both settings (see table in §6.2).

### 10.2 Deploy order

1. **Backend** (flag = `true`): accepts both old and new clients.
2. Bake; watch telemetry for `mode` mix.
3. **Client** (the new code is already targeting the new server).
4. Bake until `mode=legacy` volume drops to near-zero.
5. **Flip flag to `false`.** No redeploy.

### 10.3 Rollback levers

- **Client regression:** revert the PWA blob-storage bundle (one-command rollback). Flag stays `true`. Any still-reachable old-client flow keeps working.
- **Backend regression in hashed-mode path:** flip `Features.IgnoreChunkHashHeader` to `true` (declared in §10.1, default `false`). The server then ignores the `X-SafeExchange-Chunk-Hash` header entirely and routes every request through legacy mode; commit endpoint returns 422. Client maps 422 to "integrity temporarily disabled — your upload succeeded as a legacy attachment" (graceful degradation). Flipping `AllowLegacyAttachmentUploads` would be the wrong direction — that flag is about whether to *accept* header-less uploads, not about whether to *process* the header when present.
  - Mid-flight uploads with `RunningHashState` populated at the time of flag flip: next chunk's inferred mode becomes "inconsistent" → 400. Client restarts the attachment. Rare, transient.

### 10.4 Migration

One EF Core migration in the backend: `AddAttachmentIntegrityColumns`. Up: add `ContentMetadata.Hash`, `ContentMetadata.RunningHashState`. Down: drop columns. No data migration.

---

## 11. Testing strategy

### 11.1 Hand-rolled `SerializableSha256` (highest stakes)

- **NIST FIPS 180-4 CAVS vectors:** empty string → `e3b0c442…`, `"abc"` → `ba7816bf…`, 448-bit / 896-bit / 1 000 000-`a` test.
- **BCL cross-check, per-input:** `SerializableSha256.HashAll(x) == SHA256.HashData(x)` byte-for-byte. Every test case runs both, asserts equality.
- **State-serialisation property:** for random `x` and every split point `k ∈ [0, x.Length]`: `hash(x) == (hasher.Append(x[..k]); state = hasher.SaveState(); h2 = Restore(state); h2.Append(x[k..]); h2.Finish())`. Several thousand iterations with `Random`.
- **Multi-save property:** save → restore → save → restore. Detects state corruption in the codec.
- **Buffer-boundary edge cases:** inputs of length 0, 1, 55, 56, 63, 64, 65, 127, 128, 129, 4096.
- **Fuzz:** 1000 iterations × 100k random bytes, cross-check against BCL.
- **Coverage goal:** 100% branch, 100% BCL parity.

### 11.2 Backend unit tests (xUnit, in the existing test project)

- Chunk-upload handler: happy path hashed-mode, happy path legacy-mode (flag on, no header), 400 on header absent with flag off, 400 on inconsistent mode, per-chunk hash mismatch (422, blob deleted, state unchanged), main-content path unchanged.
- Commit handler: happy path, 422 no_upload_state, 422 legacy-mode content, 422 hash_mismatch, idempotent second commit (422 no_upload_state since state was cleared), access-ticket mismatch, access-ticket timeout clears `RunningHashState`.

### 11.3 Backend integration tests (in-memory Functions host + in-memory DB)

- 3-chunk happy upload + commit.
- 3-chunk upload with chunk 1 body tampered → 422 → client retries → success.
- Commit with wrong hash → 422 → client gives up.
- Legacy-mode full upload (no headers) → `Ready` on final chunk → no commit call.
- Reader GET returns `ContentMetadataOutput.Hash` populated, chunk hashes populated.

### 11.4 Client unit tests (Blazor component lib test project)

- `UploadAttachmentsAsync` emits `X-SafeExchange-Chunk-Hash` matching body bytes, per chunk.
- `UploadAttachmentsAsync` calls `CommitContentAsync` with the correct aggregate hash after the last chunk.
- `CommitContentAsync` marks the attachment as `Success` on 200, as `Error` on 422.
- Verified-download path (mocked `HttpClient`): happy path sets the verified badge; per-chunk mismatch aborts the writable + fails loudly; whole-content mismatch aborts + fails loudly.
- Local-file verify: match + mismatch paths; empty file; 10 MB file.

### 11.5 Playwright E2E (against staging)

- Sign in → upload a ~1 MB attachment → sign out → sign in as co-user with read access → download → status badge flips to verified → saved file's `sha256sum` equals the badge's hash.
- Upload → click "Verify local file…" → pick the same file → "matches" toast.
- Upload → click "Verify local file…" → pick a different file with the same name → "does not match" toast.

### 11.6 Out of scope for automated testing

- Multi-GB uploads (CI cost vs. value).
- MITM browser-layer simulation (covered by server unit tests that simulate tampered bodies).
- Firefox FSA fallback beyond a smoke test.

---

## 12. Open questions / future work

- **Backfilling hashes for legacy attachments.** Current design intentionally does not. If we ever want it, it's a follow-up feature with its own design (attested vs. best-effort, UI migration, user-facing messaging).
- **Resumable uploads.** Out of scope. The design naturally accommodates per-chunk retry but doesn't persist chunk state across sessions.
- **End-to-end encryption.** Separate design (`docs/owasp/A04-cryptographic-failures.md` P4 #1). Would layer above integrity.
- **`SerializableSha256` → `SerializableSha512` or similar.** If we ever want stronger margin, the class structure is designed to generalise.

---

## 13. File inventory

### Client repo (this repo, `features/attachments-integrity`)

- `SafeExchange.Client.Common/ApiClient/ApiClient.cs` — add `CommitContentAsync`, thread the `chunkHash` arg through `PutSecretDataStreamAsync`, revise `UploadAttachmentsAsync`.
- `SafeExchange.Client.Common/ApiClient/SecretContentStream.cs` — gain chunk-hash verification; may be retired in favour of a new streaming-verify helper.
- `SafeExchange.Client.Common/Model/ContentMetadata.cs` — add `Hash`.
- `SafeExchange.Client.Web.Components/Classes/Helpers/DownloadUploadHelper.cs` — rework download entry point to call the new verified-download flow.
- `SafeExchange.Client.Web.Components/Pages/ViewData.razor` — attachment row UI (hash cell, badge, kebab menu, verify-local action).
- `SafeExchange.Client.Web.Components/wwwroot/js/saexDownload.js` — new JS helper: `startVerifiedSave`, `writeBlock`, `close`, `abort` with FSA primary + in-memory fallback.
- `SafeExchange.Client.Web.Components/wwwroot/js/saexHash.js` — new JS helper (if needed for local-file verify streaming; may be replaceable by direct .NET `InputFile.OpenReadStream`).
- Test project: add `ApiClientUploadTests`, `DownloadVerifyTests`, `LocalVerifyTests`.
- `docs/superpowers/specs/2026-04-19-attachments-integrity-design.md` — this file.

### Backend repo (`C:\Users\yurio\Documents\github\safeexchange`, `features/attachments-integrity`)

- `SafeExchange.Core/Crypto/SerializableSha256.cs` — new.
- `SafeExchange.Core/Crypto/SerializableSha256Tests.cs` (or corresponding test-project path) — new.
- `SafeExchange.Core/Functions/SafeExchangeSecretStream.cs` — revise `HandleSecretStreamUpload` for hashed/legacy mode + mode-lock.
- `SafeExchange.Core/Functions/SafeExchangeContentCommit.cs` — new commit function.
- `SafeExchange.Functions/Functions/SafeContentCommit.cs` — HTTP trigger glue.
- `SafeExchange.Core/Model/ContentMetadata.cs` — add `Hash`, `RunningHashState`.
- `SafeExchange.Core/Model/Dto/Output/ContentMetadataOutput.cs` — add `Hash`.
- `SafeExchange.Core/Configuration/Features.cs` — add `AllowLegacyAttachmentUploads`, `IgnoreChunkHashHeader`.
- `SafeExchange.Core/Migrations/XXXX_AddAttachmentIntegrityColumns.cs` — new EF migration.
- `SafeExchange.Core/Purger/...` — ensure `RunningHashState` is cleared alongside chunks on expiry.
- Backend test project: add handler + commit + `SerializableSha256` tests.
