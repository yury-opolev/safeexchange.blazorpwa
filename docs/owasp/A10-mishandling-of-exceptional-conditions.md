# A10:2025 — Mishandling of Exceptional Conditions

**Findings:** 9 · **Highest priority:** P1

---

## [P1] [HIGH] `CreateFromCompoundModelAsync` silently returns "ok" when grant-access step fails (partial-commit)

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-754 (Improper Check for Unusual or Exceptional Conditions), CWE-755 (Improper Handling of Exceptional Conditions)
- **File:** `SafeExchange.Client.Common/ApiClient/ApiClient.cs:146-206`
- **Severity:** High · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** Confirmed · **Priority: P1**

**Evidence:**

```csharp
await this.UploadAttachmentsAsync(input.Metadata.ObjectName, attachments);

var permissions = input.Permissions.Where(p => !string.IsNullOrWhiteSpace(p.SubjectId)).ToList();
if (permissions.Count > 0)
{
    var accessReply = await this.GrantAccessAsync(
        input.Metadata.ObjectName, permissions.Select(p => p.ToDto()).ToList());
    if (!"ok".Equals(accessReply.Status))
    {
        // TODO ...
    }
}

return new BaseResponseObject<string>()
{
    Status = "ok",
    Result = "ok"
};
```

**Description:** `if (!"ok".Equals(accessReply.Status)) { // TODO ... }` is literal detection-without-action. After metadata and content have been written, if `GrantAccessAsync` fails, the function still returns `Status = "ok"`. `UploadAttachmentsAsync` also swallows per-attachment exceptions (lines 260-264 — `catch (Exception) { attachment.Status = UploadStatus.Error; }`), and the outer function never inspects `attachment.Status`. Every attachment can fail and the secret is still reported as "created successfully".

**Attack Scenario:**

1. User A fills the "create new secret" form with sensitive data, intending to share with `alice@corp`. Form also adds file attachments.
2. Backend `GrantAccess` call fails (transient 5xx, auth token expires mid-operation, RBAC backend misconfiguration). Secret content has already been written.
3. Client shows "created successfully" toast and navigates. User A believes intended subjects can read it.
4. Actual state: only the creator has access. Intended recipients don't. User A never retries the grant — silent integrity failure.
5. **Attacker-flavored variant:** an attacker who can drop the `/access/` PUT (via targeted transient interference) can deliberately cause access grants to fail while the secret is created, leaving the sender under the impression that the recipient has it. "Store without share" is the secrets-sharing analog of "debit without credit".

**Recommendation:** Return a non-`ok` status with a structured error listing what succeeded and what failed; surface to the caller so the UI can prompt retry or clean up. Ideally delete the freshly created secret on grant failure (compensating action) or keep the user on the form.

**Assumption:** If the backend grants the creator default access and the only failure mode is granting additional subjects, the worst case is silent sharing failure (still integrity). If the backend leaves the secret owner-less on grant failure, severity rises to Critical.

---

## [P1] [HIGH] `DownloadToFileStreamAsync` commits partial file on mid-stream exception and upgrades status to Success

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-755, CWE-460 (Improper Cleanup on Thrown Exception)
- **File:** `SafeExchange.Client.Web.Components/Pages/ViewData.razor:375-449`
- **Severity:** High · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P1**

**Evidence:**

```csharp
catch (Exception exception)
{
    this.Notification = new NotificationData() { ..., Message = exception.Message };
}
finally
{
    if (writableStream != default)
    {
        await this.downloadUploadHelper.FinishFileDownloadAsync(writableStream);  // line 446 — unconditional commit
    }
}
```

```csharp
// outer finally in DownloadAttachmentAsync at line 364
if (downloadItem.Status == DownloadStatus.InProgress)
{
    downloadItem.Status = DownloadStatus.Success;    // silently upgrades InProgress → Success
}
```

**Description:** The `finally` block unconditionally calls `FinishFileDownloadAsync(writableStream)`, which closes the FileSystemAccess writable stream. Per browser FileSystemAccess semantics, closing a writable stream commits whatever bytes were written. So if a chunk fetch fails mid-attachment, the user is left with a **truncated secret file on disk** while only seeing a yellow toast with the exception message. Additionally, the outer `DownloadAttachmentAsync` `finally` upgrades `Status = InProgress → Success`, so the UI tells the user the download succeeded.

**Attack Scenario:**

1. User clicks download on a multi-chunk attachment.
2. After the first chunk has been streamed to the writable file, the backend token times out or returns 401 on the second chunk.
3. `WriteToFileAsync` throws; catch sets a warning notification with `exception.Message`.
4. `finally` closes the writable stream, committing only the first chunk to disk.
5. Outer `DownloadAttachmentAsync` `finally` upgrades `Status` from `InProgress` to `Success`. UI shows a green check.
6. User uses this partial file as if it were the full secret — e.g., a base64-encoded private key or configuration file — and the downstream consumer silently malfunctions or (worse) accepts the truncated content as authentic.

A targeted attacker who can cause intermittent chunk-fetch failures (or controls a proxy) can induce partial downloads on every attempt while the victim never sees an error signal strong enough to reject the file.

**Recommendation:** Introduce a `success` flag inside the `try`. In the `finally`, if the flag is false, call `writableStream.abort()` (FileSystemAccess has an abort method) instead of `close()`, so the partial file is discarded. Fix the outer `DownloadAttachmentAsync` — on exception, Status must be `Error`, not `Success`.

---

## [P1] [HIGH] Silent `// no-op` swallows `DeleteContentMetadataAsync` failures in `TryUpdateSecretAsync`

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-754, CWE-755
- **File:** `SafeExchange.Client.Web.Components/Pages/EditData.razor:803-810`
- **Severity:** High · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** Confirmed · **Priority: P1**

**Evidence:**

```csharp
foreach (var deletedAttachment in this.DeletedAttachments)
{
    var contentDeletionResult = await apiClient.DeleteContentMetadataAsync(
        this.compoundModel.Metadata.ObjectName, deletedAttachment.ContentName);
    if (!"ok".Equals(contentDeletionResult.Status))
    {
        // no-op
    }
}
```

**Description:** The UI shows "Secret updated successfully" on completion even if every attempted attachment deletion returned a non-`ok` status. For a secrets-sharing app, the user's mental model is "I deleted sensitive attachment X from this secret". If the backend rejects the delete and the client silently ignores it, the attachment remains accessible to every authorized user — a confidentiality regression the user will never learn about.

**Attack Scenario:**

1. Employee B leaves the company. User A edits a shared secret to delete `offboarding-keys.txt`.
2. Backend delete returns 403 (retention lock, permission subset issue, backend bug).
3. Client shows "updated successfully". User A believes the file is gone.
4. Former employee B (still has read access on the parent secret because permission removal happened in a separate step that may have also failed) can still download `offboarding-keys.txt`.

**Recommendation:** Aggregate delete failures into the `missingAccess`-style warning structure; do not report the update as fully successful if any content deletion failed. Prefer: abort the whole update on delete failure, or mark the per-attachment row with an error and block navigation.

---

## [P2] [MEDIUM] `StateContainer` pre-clears user-visible lists then falls through on non-ok responses

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-754, CWE-1188 (Initialization with Insufficient Validation)
- **File:** `SafeExchange.Client.Web.Components/Classes/StateContainer.cs:88-243`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** Confirmed · **Priority: P2**

**Evidence:**

```csharp
this.IncomingAccessRequests = new List<AccessRequest>();   // line 109 — pre-cleared
this.OutgoingAccessRequests = new List<AccessRequest>();

try
{
    var requests = await apiClient.GetAccessRequestsAsync();
    if (requests.Status == "ok")
    {
        ...
    }
    else
    {
        // no-op           // line 132
    }
    ...
}
```

Same pattern in `TryFetchRegisteredApplications` (line 170 `// no-op`), `TryFetchRegisteredGroups` (line 201 `// no-op`), `TryFetchPinnedGroups` (line 233 `// no-op`).

**Description:** Two access-request lists are reset to empty *before* the try. If the API returns any non-ok status (403, 500, 503), the lists remain empty and `OnAccessRequestsFetched` is still fired. Callers (e.g. `AccessRequests.razor`) bind directly to the list — the user sees a blank "Access Requests" page as if no pending requests existed. A user with pending incoming requests may unknowingly leave them open. Conversely, a revocation workflow could show "no outgoing requests" and cause the user to stop tracking.

The other three methods don't pre-clear, so they preserve stale data on failure — which is a different fail-open: user sees data that isn't refreshed, without being told.

**Recommendation:** On non-ok status, surface a clear notification ("Could not load pending access requests — retry"), keep in-memory lists as `null` (not empty) so the UI can distinguish "loading failed" from "none", and persist the failure in `DataFetchedEventArgs`.

---

## [P2] [MEDIUM] `async void OnTimerElapsed` in `ItemSearchDialog` has no exception boundary

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-755, CWE-248 (Uncaught Exception)
- **File:** `SafeExchange.Client.Web.Components/Shared/ItemSearchDialog.razor:323-341` (entry), chain ending at `SearchItemsAsync:357-395`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P2**

**Evidence:**

```csharp
private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
{
    if (!this.QualifiesAsNewSearch(this.searchString)) return;
    ...
    await this.SearchItemsAsync(this.latestSearchString);
}
```

**Description:** `SearchItemsAsync` only catches `AccessTokenNotAvailableException`. Any other exception (`HttpRequestException`, `JsonException`, `NullReferenceException`, `ObjectDisposedException`) propagates out. Because the caller is `async void` (an `ElapsedEventHandler`), the exception is not observed by any `Task`. On .NET Timer's contract, unhandled exceptions in `Elapsed` handlers are silently swallowed by the timer callback wrapper. Result: the search silently stops working, no notification, no log.

**Attack Scenario:** A user searches for a subject to grant access. An attacker who can cause the search endpoint to return malformed JSON triggers a `JsonException`. The search appears to return no results. User concludes "alice@corp is not a valid subject", defaults to "group: everyone", and grants broad access they did not intend. Sharing is re-routed through the error path into over-permissive state.

**Recommendation:** Wrap the body of `OnTimerElapsed` in a try/catch that displays a generic error notification on any `Exception`. Add a catch-all `catch (Exception)` to `SearchItemsAsync` that logs and updates `this.Notification` with a user-visible warning.

---

## [P2] [MEDIUM] `SecretContentStream.Read` uses sync-over-async in Blazor WASM

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-755, CWE-833 (Deadlock)
- **File:** `SafeExchange.Client.Common/ApiClient/SecretContentStream.cs:113`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** High · **Priority: P2**

**Evidence:**

```csharp
public override int Read(byte[] buffer, int offset, int count)
{
    if (this.currentChunk == null || this.currentChunkPosition == this.currentChunk.Length)
    {
        this.currentChunkIndex += 1;
        this.currentChunk = this.chunks[this.currentChunkIndex];
        this.currentChunkPosition = 0;
        this.currentSourceStream = this.GetCurrentChunkStreamAsync().GetAwaiter().GetResult();
    }
    ...
}
```

**Description:** Blazor WebAssembly runs on a single-threaded JS event loop. Calling `.GetAwaiter().GetResult()` on a pending Task deadlocks — the continuation can never resume because the single thread is blocked. Additionally, `currentChunkIndex` is incremented *before* the chunk fetch succeeds. If the fetch throws, a subsequent read attempt will skip past the failed chunk without retry and will run off the end of `this.chunks` (`ArgumentOutOfRangeException` on line 111).

**Recommendation:** Do not use sync-over-async in Blazor WASM. Expose `ReadAsync` and have callers use async stream copying. On chunk fetch failure, do not advance `currentChunkIndex` until the fetch succeeds; propagate the exception cleanly.

**Assumption:** If `SecretContentStream` is never instantiated at runtime (dead code / server-only), severity drops to Low.

---

## [P2] [MEDIUM] `UploadAttachmentsAsync` catch-all swallows exceptions; callers never inspect `attachment.Status`

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-755, CWE-390 (Detection of Error Condition Without Action)
- **Files:** `SafeExchange.Client.Common/ApiClient/ApiClient.cs:208-266`; callers at `ApiClient.cs:188` and `EditData.razor:812`
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Authenticated · **Confidence:** Confirmed · **Priority: P2**

**Evidence:**

```csharp
try
{
    attachment.Status = UploadStatus.InProgress;
    ...
}
catch (Exception exception)
{
    attachment.Status = UploadStatus.Error;
    attachment.Error = $"{exception.GetType()}: {exception.Message}";
}
```

**Description:** Neither `CreateFromCompoundModelAsync` nor `TryUpdateSecretAsync` reads `attachment.Status` after `UploadAttachmentsAsync` returns. Every attachment can fail (including `OutOfMemoryException`, `JSException` from reading the browser File, mid-stream `HttpRequestException`) and the caller reports "Secret created/updated successfully". Combined with P1 finding A10-1, this means the create-secret + attach-files + grant-access workflow has three silently failing stages.

**Recommendation:** After `UploadAttachmentsAsync`, iterate and aggregate errors. Fail the parent operation or warn the user loudly if any attachment is in `UploadStatus.Error`. Narrow the catch-all to specific exception types; let programmer errors (NRE, InvalidOperationException) propagate.

---

## [P3] [LOW] `ProcessResponseAsync` returns raw exception type/message and raw response body as user-visible error strings

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-209 (Generation of Error Message Containing Sensitive Information), CWE-755
- **File:** `SafeExchange.Client.Common/ApiClient/ApiClient.cs:526-563`
- **Severity:** Low · **Exploitability:** Easy · **Exposure:** Internet (user's own session) · **Confidence:** High · **Priority: P3**

**Description:** The catch-all returns the *entire raw response body* as the UI error string if deserialization fails, and otherwise returns the exception type + message. Multiple Razor pages bind this `Error` string directly into `NotificationData.Message` and render it in a toast. In a WASM app this stays in-browser, so there's no server-to-client leak — but:

- A backend bug returning a stack trace in the body is now surfaced to the user in its entirety.
- Exception types from the .NET runtime (`System.Text.Json.JsonReaderException: '<' is invalid at line 1 position 1`) leak backend implementation details.
- The catch-all catches programmer errors (NRE, ArgumentException) and reports them with their `.GetType()` name visible to the user.

**Recommendation:** Map caught exceptions to a fixed set of user-friendly messages ("Network error", "Server error (code N)", "Could not parse server response"). Log full details via `ILogger`. Never inline raw response bodies into notification strings.

---

## [P3] [LOW] `AddFiles` file-reader catch swallows and only writes to `Console.WriteLine`

- **Category:** A10:2025 — Mishandling of Exceptional Conditions
- **CWE:** CWE-391 (Unchecked Error Condition), CWE-778
- **File:** `SafeExchange.Client.Web.Components/Pages/EditData.razor:568-587`; same pattern in `CreateData.razor:451-469`
- **Severity:** Low · **Exploitability:** Easy · **Exposure:** Authenticated · **Confidence:** Confirmed · **Priority: P3**

**Evidence:**

```csharp
catch (Exception exception)
{
    Console.WriteLine($"{exception.GetType()}: {exception.Message}");
}
```

**Description:** If constructing `InputBrowserFileModel` throws for one file in a multi-file selection, the user is not informed — the file is silently dropped and the loop continues. User thinks they attached 5 files; actually only 4 because file 3 had a character in its name that triggered an exception.

**Recommendation:** Show a warning notification listing the files that could not be attached. Log full exception details via `ILogger`, not `Console.WriteLine`.
