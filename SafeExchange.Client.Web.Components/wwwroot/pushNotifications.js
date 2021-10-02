
async function isPushManagerAvailable() {
    var pushManager = await getPushManager();
    return !(pushManager == null);
}

async function requestSubscription(applicationServerPublicKey) {
    const pushManager = await getPushManager();
    if (!pushManager) {
        console.log("pushManager not available via web standard.");
        return null;
    }

    const existingSubscription = await pushManager.getSubscription();
    if (existingSubscription) {
        return {
            url: existingSubscription.endpoint,
            p256dh: arrayBufferToBase64(existingSubscription.getKey('p256dh')),
            auth: arrayBufferToBase64(existingSubscription.getKey('auth'))
        };
    }

    const subscription = await subscribe(pushManager, applicationServerPublicKey);
    if (subscription) {
        return {
            url: subscription.endpoint,
            p256dh: arrayBufferToBase64(subscription.getKey('p256dh')),
            auth: arrayBufferToBase64(subscription.getKey('auth'))
        };
    }
}

async function getSubscription() {
    const pushManager = await getPushManager();
    if (!pushManager) {
        console.log("pushManager not available via web standard.");
        return null;
    }

    const existingSubscription = await pushManager.getSubscription();
    if (!existingSubscription) {
        return null;
    }
    return {
        url: existingSubscription.endpoint,
        p256dh: arrayBufferToBase64(existingSubscription.getKey('p256dh')),
        auth: arrayBufferToBase64(existingSubscription.getKey('auth'))
    };
}

async function deleteSubscription() {
    const pushManager = await getPushManager();
    if (!pushManager) {
        console.log("pushManager not available via web standard.");
        return false;
    }

    const existingSubscription = await pushManager.getSubscription();
    if (!existingSubscription) {
        return false;
    }
    return await existingSubscription.unsubscribe();
}

async function subscribe(pushManager, applicationServerPublicKey) {
    try {
        return await pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: applicationServerPublicKey
        });
    } catch (error) {
        if (error.name === 'NotAllowedError') {
            return null;
        }
        throw error;
    }
}

async function getPushManager() {
    const worker = await window.navigator.serviceWorker.getRegistration();
    return worker.pushManager;
}

function arrayBufferToBase64(buffer) {
    var binary = '';
    var bytes = new Uint8Array(buffer);
    var len = bytes.byteLength;
    for (var i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return window.btoa(binary);
}

export { isPushManagerAvailable, requestSubscription, getSubscription, deleteSubscription };
