
function addTooltips(initialText, clickText) {
    $('[data-toggle="tooltip"]').tooltip();
    $('[data-toggle="tooltip"]').on('click', function () {
        $(this).attr("title", clickText).tooltip("_fixTitle").tooltip("show").attr("title", initialText).tooltip("_fixTitle");
    });
}

export { addTooltips };