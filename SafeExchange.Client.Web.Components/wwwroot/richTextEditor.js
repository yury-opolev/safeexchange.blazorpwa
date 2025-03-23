
const EmbedBlot = Quill.import('blots/embed');

class PasswordBlot extends EmbedBlot {
    static create(value) {
        const node = super.create(value);
        node.setAttribute("data-value", value);

        const copyToClipboardButton = document.createElement("button");
        copyToClipboardButton.className = "btn d-inline-flex btn-sm";
        copyToClipboardButton.setAttribute("type", "button");
        copyToClipboardButton.setAttribute("data-bs-toggle", "tooltip");
        copyToClipboardButton.setAttribute("data-bs-placement", "top");
        copyToClipboardButton.setAttribute("title", "Copy to Clipboard");

        const buttonIcon = document.createElement("span");
        buttonIcon.className = "saex-copy";

        copyToClipboardButton.appendChild(buttonIcon);

        const innerSpan = document.createElement("span");
        innerSpan.innerHTML = " ******* ";

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

class CopyableBlot extends EmbedBlot {
    static create(value) {
        const node = super.create(value);
        node.setAttribute("data-value", value);

        const copyToClipboardButton = document.createElement("button");
        copyToClipboardButton.className = "btn d-inline-flex btn-sm";
        copyToClipboardButton.setAttribute("type", "button");
        copyToClipboardButton.setAttribute("data-bs-toggle", "tooltip");
        copyToClipboardButton.setAttribute("data-bs-placement", "top");
        copyToClipboardButton.setAttribute("title", "Copy to Clipboard");

        const buttonIcon = document.createElement("span");
        buttonIcon.className = "saex-copy";

        copyToClipboardButton.appendChild(buttonIcon);

        const innerSpan = document.createElement("span");
        innerSpan.innerHTML = value;

        const outerSpan = document.createElement("span");
        outerSpan.className = "border-bottom border-secondary border-2 ql-copyable-span";
        outerSpan.appendChild(innerSpan);
        outerSpan.appendChild(copyToClipboardButton);

        node.appendChild(outerSpan);

        return node;
    }

    static value(node) {
        return node.getAttribute("data-value");
    }
}
CopyableBlot.blotName = 'copyable';
CopyableBlot.tagName = 'SPAN';
CopyableBlot.className = "ql-copyable-holder";

Quill.register(CopyableBlot, true);

Quill.register('modules/blotFormatter2', QuillBlotFormatter2.default);

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
            container: '#quill-toolbar',
            handlers: {
                'password': async function (value) {
                    if (value) {
                        var range = this.quill.getSelection();
                        if (range == null || range.length === 0) {
                            return;
                        }
                        var text = this.quill.getText(range);
                        var position = range.index;
                        this.quill.deleteText(position, range.length)
                        this.quill.insertEmbed(position, 'password', text, Quill.sources.USER);
                        this.quill.setSelection(position + 1, Quill.sources.API);
                        await dotNetRef.invokeMethodAsync("OnCopyableElementInsertedJS");
                    }
                },
                'copyable': async function (value) {
                    if (value) {
                        var range = this.quill.getSelection();
                        if (range == null || range.length === 0) {
                            return;
                        }
                        var text = this.quill.getText(range);
                        var position = range.index;
                        this.quill.deleteText(position, range.length)
                        this.quill.insertEmbed(position, 'copyable', text, Quill.sources.USER);
                        this.quill.setSelection(position + 1, Quill.sources.API);
                        await dotNetRef.invokeMethodAsync("OnCopyableElementInsertedJS");
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
        blotFormatter2: {
            align: {
                allowAligning: true,
            },
            resize: {
                allowResizing: true,
            },
            delete: {
                allowKeyboardDelete: true,
            },
            image: {
                allowAltTitleEdit: false,
                allowCompressor: false
            }
        }
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
    const quill = Quill.find(quillElement);
    if (!quill) {
        return;
    }
    quill.enable(enabled);
}

function getContents(quillElement) {
    const quill = Quill.find(quillElement);
    return quill.getContents();
}

function setContents(quillElement, contentsToSet) {
    const quill = Quill.find(quillElement);
    quill.setContents(contentsToSet, 'api');
}

function getHtml(quillElement) {
    const quill = Quill.find(quillElement);
    return quill.root.innerHTML;
}

function setHtml(quillElement, htmlToSet) {
    const quill = Quill.find(quillElement);
    if (!quill) {
        return;
    }
    const delta = quill.clipboard.convert(htmlToSet);
    quill.setContents(delta, 'api');
}

function getText(quillElement) {
    const quill = Quill.find(quillElement);
    return quill.getText();
}

function setText(quillElement, textToSet) {
    const quill = Quill.find(quillElement);
    quill.setText(textToSet, 'api');
}

export { initializeEditor, setEnabled, getContents, setContents, getHtml, setHtml, getText, setText };