// Shrinks the pictures a user picked for "Create from a picture" before they are uploaded.
// Used only through IMealPictureShrinker (Features/MealImport/State/MealPictureShrinker.cs).

// Reads one file and returns it as a smaller JPEG: orientation applied to the pixels, the long edge at most
// maxEdge px (never scaled up), compressed at the given quality. Throws when the browser can't decode the file.
async function shrinkFile(file, maxEdge, quality) {
    // "from-image" applies the EXIF orientation, so the canvas must not rotate again.
    const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
    try {
        const scale = Math.min(1, maxEdge / Math.max(bitmap.width, bitmap.height));
        const width = Math.max(1, Math.round(bitmap.width * scale));
        const height = Math.max(1, Math.round(bitmap.height * scale));

        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext("2d");
        // JPEG has no alpha: a transparent PNG would otherwise turn black.
        context.fillStyle = "#ffffff";
        context.fillRect(0, 0, width, height);
        context.drawImage(bitmap, 0, 0, width, height);

        const blob = await new Promise((resolve, reject) =>
            canvas.toBlob(b => (b ? resolve(b) : reject(new Error("toBlob failed"))), "image/jpeg", quality));
        return blob;
    } finally {
        bitmap.close();
    }
}

// Blazor only passes a Uint8Array as a top-level argument or result, not inside an object, so nested bytes go as base64.
function toBase64(blob) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result).split(",")[1]);
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(blob);
    });
}

// The first maxCount files of the <input type="file">, each as { ok, bytes (base64), previewUrl }. A file that can't be
// decoded or shrunk is { ok: false }. Clears the input afterwards so the same file can be chosen again.
export async function shrinkSelectedFiles(input, maxCount, maxEdge, quality) {
    const files = Array.from(input.files ?? []).slice(0, maxCount);
    const results = [];
    for (const file of files) {
        try {
            const blob = await shrinkFile(file, maxEdge, quality);
            results.push({ ok: true, bytes: await toBase64(blob), previewUrl: URL.createObjectURL(blob) });
        } catch (error) {
            console.warn("A picture could not be shrunk:", error);
            results.push({ ok: false, bytes: null, previewUrl: null });
        }
    }
    input.value = "";
    return results;
}

export function revokePreview(url) {
    URL.revokeObjectURL(url);
}
