
async function requestSubscription(applicationServerPublicKey) {
    const worker = await window.navigator.serviceWorker.getRegistration();
    const existingSubscription = await worker.pushManager.getSubscription();
    if (existingSubscription) {
        return {
            url: existingSubscription.endpoint,
            p256dh: arrayBufferToBase64(existingSubscription.getKey('p256dh')),
            auth: arrayBufferToBase64(existingSubscription.getKey('auth'))
        };
    }

    const subscription = await subscribe(worker, applicationServerPublicKey);
    if (subscription) {
        return {
            url: subscription.endpoint,
            p256dh: arrayBufferToBase64(subscription.getKey('p256dh')),
            auth: arrayBufferToBase64(subscription.getKey('auth'))
        };
    }
}

async function getSubscription() {
    const worker = await window.navigator.serviceWorker.getRegistration();
    const existingSubscription = await worker.pushManager.getSubscription();
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
    const worker = await window.navigator.serviceWorker.getRegistration();
    const existingSubscription = await worker.pushManager.getSubscription();
    if (!existingSubscription) {
        return false;
    }
    return await existingSubscription.unsubscribe();
}

async function subscribe(worker, applicationServerPublicKey) {
    try {
        return await worker.pushManager.subscribe({
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

function arrayBufferToBase64(buffer) {
    var binary = '';
    var bytes = new Uint8Array(buffer);
    var len = bytes.byteLength;
    for (var i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return window.btoa(binary);
}

export { requestSubscription, getSubscription, deleteSubscription };
