// Small UI helpers for the main PWA. The CSP forbids 'unsafe-eval' and
// 'unsafe-inline', so Blazor JS-interop can't dispatch chained calls like
// bootstrap.Dropdown.getInstance(el).hide() via eval — they need a real
// function on window.

window.safeexchange = window.safeexchange || {};

// Hide the user-info dropdown if it's currently open. Used after the
// "My S2S apps" entry navigates away — Bootstrap's data-bs-auto-close
// is set to "outside" so inner clicks don't dismiss it automatically.
window.safeexchange.closeUserDropdown = function () {
    var el = document.getElementById('userInfoDropdown');
    if (!el || !window.bootstrap || !window.bootstrap.Dropdown) {
        return;
    }
    var instance = window.bootstrap.Dropdown.getInstance(el);
    if (instance) {
        instance.hide();
    }
};

window.safeexchange.showModal = function (el) {
    if (!el || !window.bootstrap || !window.bootstrap.Modal) { return; }
    bootstrap.Modal.getOrCreateInstance(el).show();
};

window.safeexchange.hideModal = function (el) {
    if (!el || !window.bootstrap || !window.bootstrap.Modal) { return; }
    var instance = bootstrap.Modal.getInstance(el);
    if (instance) { instance.hide(); }
};
