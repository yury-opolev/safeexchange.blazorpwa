// Small Bootstrap-modal helpers used by ConfirmDialog.razor so Blazor can
// open/close a modal without chained interop calls.

window.adminUi = window.adminUi || {};

window.adminUi.showModal = function (el) {
    if (!el || !window.bootstrap || !window.bootstrap.Modal) {
        return;
    }
    bootstrap.Modal.getOrCreateInstance(el).show();
};

window.adminUi.hideModal = function (el) {
    if (!el || !window.bootstrap || !window.bootstrap.Modal) {
        return;
    }
    var instance = bootstrap.Modal.getInstance(el);
    if (instance) {
        instance.hide();
    }
};
