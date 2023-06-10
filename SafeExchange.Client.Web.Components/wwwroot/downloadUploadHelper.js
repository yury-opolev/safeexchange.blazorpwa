function triggerAttachFile(element) {
    return element.click();
}

async function downloadFileFromStream(fileName, contentType, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType });

    const url = URL.createObjectURL(blob);

    triggerFileDownload(fileName, url);

    URL.revokeObjectURL(url);
}

function triggerFileDownload(fileName, url) {
    const anchorElement = document.createElement('a');
    anchorElement.href = url;

    if (fileName) {
        anchorElement.download = fileName;
    }

    anchorElement.click();
    anchorElement.remove();
}

function supportsFileSystemAccess()
{
    if (!('showSaveFilePicker' in window)) {
        return false;
    }

    try {
        return window.self === window.top;
    } catch {
        return false;
    }
}

async function startFileWriteAsync() {
    const fileHandle = await window.showSaveFilePicker();
    const writableStream = await fileHandle.createWritable();
    return writableStream;
}

async function writeToFileStreamAsync(writableStream, contents) {
    await writableStream.write(contents);
}

async function closeFileAsync(writableStream) {
    await writableStream.close();
}

export { triggerAttachFile, downloadFileFromStream, supportsFileSystemAccess };