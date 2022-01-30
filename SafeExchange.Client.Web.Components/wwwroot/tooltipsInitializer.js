
function addTooltips(initialText, clickText) {
    var tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggerList.forEach((element) => new bootstrap.Tooltip(element));

    $('[data-bs-toggle="tooltip"]').on('click', function () {
        $(this).attr("title", clickText).tooltip("_fixTitle").tooltip("show").attr("title", initialText).tooltip("_fixTitle");
    });
}

function addQuillClipboardTooltips(quillElement, initialText, clickText) {
    var tooltipTriggerList = quillElement.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggerList.forEach((element) => {
        const content = element.parentElement.parentElement.parentElement.getAttribute("data-value");
        initClipboardTooltip(element, content, initialText, clickText);
    });
}

function initClipboardTooltip(element, content, initialText, clickText) {
    const tooltip = new bootstrap.Tooltip(element)
    element.onclick = async () => {
        await navigator.clipboard.writeText(content);
        $(element).attr("title", clickText).tooltip("_fixTitle").tooltip("show").attr("title", initialText).tooltip("_fixTitle");
    };
}

export { addTooltips, addQuillClipboardTooltips };