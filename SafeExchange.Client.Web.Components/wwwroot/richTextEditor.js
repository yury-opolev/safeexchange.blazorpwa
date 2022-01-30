
const EmbedBlot = Quill.import('blots/embed');

class PasswordBlot extends EmbedBlot {
    static create(value) {
        const node = super.create(value);
        node.setAttribute("data-value", value);

        const copyToClipboardButton = document.createElement("button");
        copyToClipboardButton.className = "btn d-inline-flex btn-sm";
        copyToClipboardButton.setAttribute("data-bs-toggle", "tooltip");
        copyToClipboardButton.setAttribute("data-bs-placement", "top");
        copyToClipboardButton.setAttribute("title", "Copy to Clipboard");

        const buttonImage = document.createElement("img");
        buttonImage.setAttribute("src", "copy.svg");
        buttonImage.setAttribute("alt", "Copy to Clipboard");
        buttonImage.setAttribute("width", "20");

        copyToClipboardButton.appendChild(buttonImage);

        const innerSpan = document.createElement("span");
        innerSpan.innerHTML = " ***** &nbsp;";

        const outerSpan = document.createElement("span");
        outerSpan.className = "border border-secondary border-2 rounded ql-password-span";
        outerSpan.appendChild(innerSpan);
        outerSpan.appendChild(copyToClipboardButton);

        node.appendChild(outerSpan);

        return node;
    }

    static value(node) {
        return node.getAttribute("data-value");
    }
}
PasswordBlot.blotName = 'password';
PasswordBlot.tagName = 'SPAN';
PasswordBlot.className = "ql-password-holder";

Quill.register(PasswordBlot, true);

function initializeEditor(dotNetRef, quillElement, placeholder, readOnly, nextElement) {
    var options = {
        placeholder: placeholder,
        readOnly: readOnly,
        theme: 'snow'
    };

    if (readOnly) {
        var toolbarOptions = false;
    } else {
        var toolbarOptions = {
            container: [
                [{ 'font': [] }],
                [{ 'size': [] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ 'align': [] }],
                [{ 'color': [] }, { 'background': [] }],
                ['link', 'image'],
                ['password'],
                ['clean']
            ],
            handlers: {
                'password': function (value) {
                    if (value) {
                        var range = this.quill.getSelection();
                        if (range == null || range.length === 0) {
                            return;
                        }
                        var text = this.quill.getText(range);
                        this.quill.deleteText(range.index, range.length)
                        this.quill.insertEmbed(range.index, 'password', text, Quill.sources.API);
                        this.quill.setSelection(range.index + 1, Quill.sources.API);
                    }
                }
            }
        };
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