
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

// Initialise a single Bootstrap tooltip on the given element with the
// same click-swap behaviour as addTooltips (title flips to clickText
// while shown, then back to initialText when hidden). Idempotent —
// marks the element after first wiring so repeat calls are no-ops.
// Pair with a C# @onclick handler when the button also needs to do
// something else on click; the click handlers co-exist.
function addTooltipFor(element, initialText, clickText) {
    if (!element || element.dataset.saexTooltipWired === "1") {
        return;
    }
    new bootstrap.Tooltip(element);
    $(element).on('click', function () {
        $(this).attr("title", clickText).tooltip("_fixTitle").tooltip("show").attr("title", initialText).tooltip("_fixTitle");
    });
    element.dataset.saexTooltipWired = "1";
}

// Plain hover-only Bootstrap tooltip — no click behaviour. Idempotent.
// Use for elements whose purpose is to reveal the full text of a
// truncated label on hover, where clicking should do nothing (or
// bubble elsewhere).
function addPlainTooltipFor(element) {
    if (!element || element.dataset.saexTooltipWired === "1") {
        return;
    }
    new bootstrap.Tooltip(element);
    element.dataset.saexTooltipWired = "1";
}

// Initialise every `[data-bs-toggle="tooltip"]` on the page as a plain
// hover-only Bootstrap tooltip. Idempotent — existing wired elements
// are skipped. Safe to call from OnAfterRenderAsync on every render.
function initAllPlainTooltips() {
    var list = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    list.forEach(addPlainTooltipFor);
}

function initClipboardTooltip(element, content, initialText, clickText) {
    const tooltip = new bootstrap.Tooltip(element)
    element.onclick = async () => {
        await navigator.clipboard.writeText(content);
        $(element).attr("title", clickText).tooltip("_fixTitle").tooltip("show").attr("title", initialText).tooltip("_fixTitle");
    };
}

export { addTooltips, addQuillClipboardTooltips, addTooltipFor, addPlainTooltipFor, initAllPlainTooltips };