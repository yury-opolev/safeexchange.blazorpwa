// Service-worker registration + update lifecycle.
//
// updateAvailable resolves once a *new* SW has finished installing AND
// there is already a current controller (i.e. this isn't the first-ever
// install — that one isn't an "update"). Resolves with the
// ServiceWorkerRegistration so callers can postMessage to the waiting
// worker without re-fetching the registration.
//
// applyUpdate is what the "Reload" toast calls. The naive
// window.location.reload() does not work for a PWA — the old SW would
// just hand back the cached old shell. We instead:
//   1. postMessage SKIP_WAITING to the waiting SW (service-worker.published.js
//      handles it via self.skipWaiting()).
//   2. Wait for navigator.serviceWorker.controllerchange — that fires
//      once the new SW becomes the page's controller.
//   3. Reload the page; the fresh request goes through the new SW.
//
// We guard against double-reload because controllerchange can fire more
// than once during the SW lifecycle (e.g. on first-ever install).

window.updateAvailable = new Promise((resolve, reject) => {
    if (!('serviceWorker' in navigator)) {
        const errorMessage = `This browser doesn't support service workers`;
        console.error(errorMessage);
        reject(errorMessage);
        return;
    }

    navigator.serviceWorker.register('/service-worker.js')
        .then(registration => {
            console.info(`Service worker registration successful (scope: ${registration.scope})`);

            // If a SW is already waiting at startup (e.g. user closed the
            // tab before clicking Reload last time), resolve immediately
            // so the toast comes back.
            if (registration.waiting && navigator.serviceWorker.controller) {
                resolve(registration);
                return;
            }

            registration.onupdatefound = () => {
                const installingServiceWorker = registration.installing;
                if (!installingServiceWorker) {
                    return;
                }

                installingServiceWorker.onstatechange = () => {
                    if (installingServiceWorker.state === 'installed' && navigator.serviceWorker.controller) {
                        // New SW installed AND something was already controlling
                        // the page → this is an update, not the first install.
                        resolve(registration);
                    }
                };
            };
        })
        .catch(error => {
            console.error('Service worker registration failed with error:', error);
            reject(error);
        });
});

window.registerForUpdateAvailableNotification = (caller, methodName) => {
    window.updateAvailable.then(() => {
        caller.invokeMethodAsync(methodName).then();
    });
};

// Called by the Reload button in UpdateAvailableDetector.razor.
window.applyUpdate = async () => {
    if (!('serviceWorker' in navigator)) {
        window.location.reload();
        return;
    }

    try {
        const registration = await navigator.serviceWorker.getRegistration();
        if (!registration || !registration.waiting) {
            // No waiting worker — nothing to swap in. Fall back to a
            // plain reload; the user clicked the button after all.
            window.location.reload();
            return;
        }

        // Reload once the new SW becomes the controller. Guard against
        // controllerchange firing more than once.
        let hasReloaded = false;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (hasReloaded) {
                return;
            }

            hasReloaded = true;
            window.location.reload();
        });

        registration.waiting.postMessage({ type: 'SKIP_WAITING' });
    } catch (error) {
        console.error('applyUpdate failed; falling back to plain reload:', error);
        window.location.reload();
    }
};
