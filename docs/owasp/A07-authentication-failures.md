# A07:2025 — Authentication Failures

**Findings:** 2 · **Highest priority:** P2

---

## [P3] [LOW] MSAL `ValidateAuthority` explicitly set to `false` for a standard Entra ID authority

- **Category:** A07:2025 — Authentication Failures (cross-category: A01/A02/A04)
- **CWE:** CWE-287 (Improper Authentication), CWE-295 (Improper Certificate/Authority Validation)
- **File:** `SafeExchange.PWA/wwwroot/appsettings.json:13-17`
- **Severity:** Low · **Exploitability:** Hard · **Exposure:** Internet · **Confidence:** Confirmed · **Priority: P3**

```json
"AzureAdB2C": {
  "Authority": "https://login.microsoftonline.com/1472703e-99d8-45ee-aeac-7ec3ae9ab104",
  "ClientId": "fdfa17b0-db9b-4927-8b83-07055ae70c39",
  "ValidateAuthority": false
}
```

See `A01-broken-access-control.md` for the detailed write-up. The section is named `AzureAdB2C` but the authority is a standard Entra ID endpoint (`login.microsoftonline.com/<tenantId>`, not B2C). MSAL's default validation works for this authority; setting the flag to `false` is unnecessary and weakens a defense-in-depth check against authority substitution.

**Recommendation:** Remove `"ValidateAuthority": false`. Rename the `AzureAdB2C` section to `EntraId` to reflect reality and prevent future maintainers from reintroducing the flag. Consolidate this fix with the A01/A02/A04 duplicates.

---

## [P2] [MEDIUM] Logout does not unsubscribe web-push registration

- **Category:** A07:2025 — Authentication Failures (logout completeness)
- **CWE:** CWE-613 (Insufficient Session Expiration), CWE-384 (Session Fixation — adjacent), CWE-1390 (Weak Authentication)
- **Files:**
  - `SafeExchange.PWA/Pages/Authentication.razor:35-39` — `OnLogOutSucceeded` handler
  - `SafeExchange.Client.Web.Components/Classes/Helpers/NotificationsSubscriber.cs:84-109` — existing `Unsubscribe` path not invoked on logout
- **Severity:** Medium · **Exploitability:** Moderate · **Exposure:** Internet · **Confidence:** High · **Priority: P2**

**Evidence:**

```csharp
public void OnLogOutSucceeded()
{
    this.StateContainerInstance.IncomingAccessRequests = null;
    this.StateContainerInstance.OutgoingAccessRequests = null;
}
```

**Description:** When a user opts into push notifications, `NotificationsSubscriber.Subscribe()` registers a browser push subscription with the backend via `apiClient.RegisterWebPushSubscriptionAsync(...)`. This creates a server-side association between the user's identity (bound at registration time) and the browser push endpoint. On sign-out, the app's only cleanup is to null out two in-memory fields on `StateContainer`. It does not:

1. Call `NotificationsSubscriber.Unsubscribe()` to tell the backend to delete the subscription.
2. Call `pushNotifications.DeleteSubscription()` to unregister at the browser level.

**Consequences:**

- **On a shared browser**, after User A logs out, the push subscription remains active. If User B signs in on the same profile, notifications intended for A (access requests, share notifications) surface to whoever is using the browser. Notifications contain subject-line metadata that may leak sensitive information (who requested access, to what secret name).
- If the backend re-issues notifications asynchronously after logout (pending requests), they are delivered to the user A believed is a clean state.
- If sign-out was a security response to credential compromise, the residual push subscription enables passive surveillance until manually revoked.

**Attack Scenario:**

1. Victim uses SafeExchange on a shared/public machine (kiosk, hot-desk). Opts into push notifications at some point.
2. Victim signs out via the "Sign out" button. `OnLogOutSucceeded` runs; MSAL clears its sessionStorage; push subscription is untouched.
3. Attacker sits down at the same browser profile. Push subscription is still active and still tied on the backend to the victim's identity.
4. A third party sends the victim an access request. Backend resolves the active push subscription and pushes to the browser.
5. The notification (with secret name / requester email visible) is displayed to the attacker on the kiosk.
6. Alternatively: victim signs back in later from another device; the old subscription keeps receiving push traffic on the kiosk indefinitely — persistent passive surveillance until the browser profile is wiped.

**Recommendation:**

1. Invoke the unsubscribe call **at the start of `BeginSignOut`** in `LoginDisplay.razor` while the user still has a valid access token (before the redirect). `OnLogOutSucceeded` executes after MSAL has cleared its cache, so a backend DELETE call there will fail.
2. Wrap it in a "best effort" try/catch so logout cannot be blocked by a failing unsubscribe.

**Assumption:** The shipped `appsettings.json` has `"AppServerPublicKey": ""`, which means the push feature is disabled-by-default and `NotificationsSubscriber` would throw at DI resolution time. This makes the finding a latent defect today — any production appsettings override that enables push re-enables the bug. Treat as P2 to catch the latent case.

---

## Clean Areas (verified)

- **MSAL registration** (`ServicesHelper.cs`): `DefaultAccessTokenScopes` correctly limited; authorized URLs in `ApiAuthorizationMessageHandler` scoped to the BackendApi base (not overly broad).
- **`ApiAuthorizationMessageHandler.cs`**: uses framework-provided `AuthorizationMessageHandler` with narrow authorized URL + narrow scope list.
- **`TokenHandler.cs`**: only reads claims (`preferred_username`, `upn`); no token storage, no logging.
- **`Authentication.razor` / `LoginDisplay.razor` / `RedirectToLogin.razor`**: standard `RemoteAuthenticatorView` + `NavigateToLogin`/`NavigateToLogout` flow; relies on MSAL state parameter validation (correct).
- **`App.razor` / `MainLayout.razor`**: `CascadingAuthenticationState` + `AuthorizeRouteView` with `NotAuthorized` fallback to `RedirectToLogin`.
- **Plaintext token logging**: no bearer/token logging in any `Classes/` file.
- **MSAL cache location**: no explicit override to `localStorage`; defaults to `sessionStorage` (tab-scoped).
- **MFA downgrade**: no custom `prompt=none` or silent flows that bypass interactive MFA requirements.
