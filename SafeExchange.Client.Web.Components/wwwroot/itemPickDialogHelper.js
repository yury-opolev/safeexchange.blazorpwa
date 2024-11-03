
function showModal(element) {
    var modal = new bootstrap.Modal(element);
    modal.show();
}

function hideModal(element) {
    var modal = new bootstrap.Modal(element);
    modal.hide();
}

export { showModal, hideModal };