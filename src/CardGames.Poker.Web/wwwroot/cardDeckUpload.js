export async function measure(input) {
    const file = input && input.files && input.files[0];
    if (!file) {
        return null;
    }

    try {
        const bitmap = await createImageBitmap(file);
        const result = {
            width: bitmap.width,
            height: bitmap.height,
            size: file.size,
            type: file.type || "",
            name: file.name,
            unreadable: false
        };
        bitmap.close();
        return result;
    } catch {
        return {
            width: 0,
            height: 0,
            size: file.size,
            type: file.type || "",
            name: file.name,
            unreadable: true
        };
    }
}

export function stream(input) {
    const file = input && input.files && input.files[0];
    // Blazor's IJSStreamReference requires a Blob or typed array, not a ReadableStream.
    return file ?? null;
}

export function clear(input) {
    if (input) {
        input.value = "";
    }
}
