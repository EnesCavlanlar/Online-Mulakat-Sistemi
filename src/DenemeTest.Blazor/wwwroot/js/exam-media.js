// window.mediaGate: Kamera/Mikrofon + Ekran paylaşımı izin/önizleme yöneticisi
window.mediaGate = (function () {
    const previews = new Map(); // elementId -> MediaStream

    async function requestCam(videoId) {
        const stream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: { echoCancellation: true, noiseSuppression: true }
        });
        attach(videoId, stream);
        return true;
    }

    async function requestScreen(videoId) {
        // Kullanıcıya her zaman seçim penceresi gelecek; kullanıcı "Tüm ekran"ı seçmeli.
        const stream = await navigator.mediaDevices.getDisplayMedia({
            video: { frameRate: 25 },
            audio: false
        });

        // "Tüm ekran" seçilmedi ise uyarı ver (bunu garantilemek tarayıcı politikası gereği %100 mümkün değil)
        try {
            const vt = stream.getVideoTracks()[0];
            const settings = vt.getSettings && vt.getSettings();
            const surf = settings && (settings.displaySurface || settings.displaySurfaceType);
            if (surf && String(surf).toLowerCase() !== "monitor") {
                // kullanıcı pencere/sekme seçmiş olabilir; yine de önizleme başlatılıyor
                console.warn("[mediaGate] Fullscreen dışında seçim yapıldı:", surf);
            }
        } catch { }

        attach(videoId, stream);
        return true;
    }

    function attach(videoId, stream) {
        const el = document.getElementById(videoId);
        if (!el) return;
        previews.set(videoId, stream);
        el.srcObject = stream;
        el.muted = true;
        el.play().catch(() => { });
    }

    // runner/recorder tarafına önizleme stream'ini devretmek için
    function pop(videoId) {
        const s = previews.get(videoId);
        if (s) previews.delete(videoId);
        return s || null;
    }

    function stopPreview(videoId) {
        try {
            const s = previews.get(videoId);
            if (s) {
                s.getTracks().forEach(t => { try { t.stop(); } catch { } });
                previews.delete(videoId);
            }
        } catch { }
    }

    return { requestCam, requestScreen, pop, stopPreview };
})();
