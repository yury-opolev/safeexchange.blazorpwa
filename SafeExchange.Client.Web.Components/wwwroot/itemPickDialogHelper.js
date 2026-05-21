
function showModal(element) {
    var modal = new bootstrap.Modal(element);
    modal.show();
}

function hideModal(element) {
    var modal = bootstrap.Modal.getInstance(element);
    if (modal) {
        modal.hide();
    }
}

export { showModal, hideModal };