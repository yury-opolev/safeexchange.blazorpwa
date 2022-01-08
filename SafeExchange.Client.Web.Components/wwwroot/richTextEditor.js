
function initializeEditor(dotNetRef, quillElement, placeholder, readOnly, nextElement) {
    var options = {
        placeholder: placeholder,
        readOnly: readOnly,
        theme: 'snow'
    };

    if (readOnly) {
        var toolbarOptions = false;
    } else {
        var toolbarOptions = [
            [{ 'font': [] }],
            [{ 'size': [] }],
            ['bold', 'italic', 'underline', 'strike'],
            [{ 'align': [] }],
            [{ 'color': [] }, { 'background': [] }],
            ['link', 'image'],
            ['clean']
        ];
    }

    var bindings = {
        // This will overwrite the default binding also named 'tab'
        tab: {
            key: 9,
            handler: function () {
                nextElement.focus();
            }
        }
    };

    options.modules = {
        keyboard: { bindings: bindings },
        toolbar: toolbarOptions,
        imageDrop: true,
        imageResize: { modules: ['Resize', 'Toolbar'] }
    };

    let quill = new Quill(quillElement, options);
    quill.on("selection-change", async function (range, oldRange, source) {
        if (range === null && oldRange !== null) {
            await dotNetRef.invokeMethodAsync("OnFocusJS");
        }
    });
    quill.on("selection-change", async function (range, oldRange, source) {
        if (range === null && oldRange !== null) {
            await dotNetRef.invokeMethodAsync("OnBlurJS");
        }
    });
    quill.on("text-change", async function (delta, oldDelta, source) {
        if (source == 'user') {
            await dotNetRef.invokeMethodAsync("OnTextChangeJS");
        }
    });
}

function setEnabled(quillElement, enabled) {
    if (!quillElement) {
        return;
    }
    const quill = quillElement.__quill;
    if (!quill) {
        return;
    }
    quill.enable(enabled);
}

function getContents(quillElement) {
    return quillElement.__quill.getContents();
}

function setContents(quillElement, contentsToSet) {
    const quill = quillElement.__quill;
    quill.setContents(contentsToSet, 'api');
}

function getHtml(quillElement) {
    return quillElement.__quill.root.innerHTML;
}

function setHtml(quillElement, htmlToSet) {
    const quill = quillElement.__quill;
    const delta = quill.clipboard.convert(htmlToSet);
    quill.setContents(delta, 'api');
}

function getText(quillElement) {
    return quillElement.__quill.getText();
}

function setText(quillElement, textToSet) {
    quillElement.__quill.setText(textToSet, 'api');
}

export { initializeEditor, setEnabled, getContents, setContents, getHtml, setHtml, getText, setText };