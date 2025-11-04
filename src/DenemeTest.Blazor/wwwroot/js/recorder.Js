window.examRecorder = (function () {
    let sessionId = null;
    let camStream = null;
    let screenStream = null;
    let mixedStream = null;
    let mediaRecorder = null;
    let seq = 0;
    let uploading = false;

    async function start(sid, timesliceMs, withScreen) {
        if (mediaRecorder) return;
        sessionId = sid;
        seq = 0;
        uploading = false;

        camStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });

        if (withScreen) {
            try {
                // Ekran + (varsa) sistem sesi
                screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: true });
            } catch (e) {
                console.warn("DisplayMedia denied, fallback to camera only", e);
                screenStream = null;
            }
        }

        mixedStream = mergeStreams(screenStream, camStream);

        mediaRecorder = new MediaRecorder(mixedStream, { mimeType: 'video/webm;codecs=vp8,opus' });

        mediaRecorder.ondataavailable = async (e) => {
            if (!e.data || e.data.size === 0) return;
            const chunk = await e.data.arrayBuffer();
            await uploadChunk(new Uint8Array(chunk));
        };

        mediaRecorder.onstop = async () => {
            try { await finalize(); } catch (e) { console.error("finalize error", e); }
            cleanup();
        };

        mediaRecorder.start(timesliceMs || 2000); // 2 sn'lik parçalar
    }

    function mergeStreams(screen, cam) {
        // öncelik ekran videosu; yoksa kamera videosu
        const tracks = [];
        if (screen && screen.getVideoTracks().length) tracks.push(screen.getVideoTracks()[0]);
        else if (cam && cam.getVideoTracks().length) tracks.push(cam.getVideoTracks()[0]);

        // ses: ekranın sesi varsa onu al, yoksa kameranın mikrofonu
        if (screen && screen.getAudioTracks().length) tracks.push(screen.getAudioTracks()[0]);
        else if (cam && cam.getAudioTracks().length) tracks.push(cam.getAudioTracks()[0]);

        return new MediaStream(tracks);
    }

    async function uploadChunk(bytes) {
        while (uploading) { await new Promise(r => setTimeout(r, 50)); }
        uploading = true;

        const url = `/api/recordings/chunk?sessionId=${encodeURIComponent(sessionId)}&seq=${seq++}`;
        await fetch(url, {
            method: 'POST',
            body: bytes,
            headers: { 'Content-Type': 'application/octet-stream' },
            credentials: 'include'
        }).catch(err => console.error('upload chunk error', err));

        uploading = false;
    }

    async function stop() {
        if (!mediaRecorder) return;
        mediaRecorder.stop();
    }

    async function cancel() {
        try {
            await fetch(`/api/recordings/cancel?sessionId=${encodeURIComponent(sessionId)}`, {
                method: 'POST',
                credentials: 'include'
            });
        } catch { }
        cleanup();
    }

    async function finalize() {
        await fetch(`/api/recordings/finalize?sessionId=${encodeURIComponent(sessionId)}&mime=${encodeURIComponent('video/webm')}`, {
            method: 'POST',
            credentials: 'include'
        });
    }

    function cleanup() {
        try {
            if (mediaRecorder && mediaRecorder.state !== 'inactive') mediaRecorder.stop();
        } catch { }
        mediaRecorder = null;

        [camStream, screenStream, mixedStream].forEach(s => {
            if (!s) return;
            s.getTracks().forEach(t => t.stop());
        });
        camStream = screenStream = mixedStream = null;
    }

    return { start, stop, cancel };
})();
