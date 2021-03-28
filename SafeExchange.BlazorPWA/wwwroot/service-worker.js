// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

self.addEventListener('push', event => event.waitUntil(onPush(event)));
self.addEventListener('notificationclick', event => event.waitUntil(onNotificationClick(event)));

async function onPush(event) {
    const payload = event.data.json();
    options = {
        body: payload.message,
        icon: 'icon-512.png',
        vibrate: [100, 50, 100],
        data: {
            dateOfArrival: Date.now(),
            url: payload.url
        },
        actions: [
            {
                action: "explore", title: "Open",
                icon: "img/checkmark.png"
            },
            {
                action: "close", title: "Close",
                icon: "img/close.png"
            },
        ]
    };
    await self.registration.showNotification('SafeExchange', options);
}

async function onNotificationClick(event) {
    event.notification.close();
    if (event.action === 'close') {
        return;
    }

    const urlToOpen = new URL(event.notification.data.url, self.location.origin).href;
    const promiseChain = clients.matchAll({
        type: 'window',
        includeUncontrolled: true
    }).then((windowClients) => {
        let matchingClient = null;

        for (let i = 0; i < windowClients.length; i++) {
            const windowClient = windowClients[i];
            if (windowClient.url === urlToOpen) {
                matchingClient = windowClient;
                break;
            }
        }

        if (matchingClient) {
            return matchingClient.focus();
        } else {
            return clients.openWindow(urlToOpen);
        }
    });

    await promiseChain;
}