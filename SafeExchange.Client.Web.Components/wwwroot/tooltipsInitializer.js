
function addTooltips(initialText, clickText) {
    var tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggerList.forEach((element) => new bootstrap.Tooltip(element));

    $('[data-bs-toggle="tooltip"]').on('click', function () {
        $(this).attr("title", clickText).tooltip("_fixTitle").tooltip("show").attr("title", initialText).tooltip("_fixTitle");
    });
}

export { addTooltips };