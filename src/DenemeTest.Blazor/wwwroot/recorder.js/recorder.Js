// Kamerayı ve ekranı AYNI ANDA kaydeder, bitince iki ayrı .webm olarak upload eder.
window.examRecorder = (function () {
    let sessionId = null;
    let camStream = null, screenStream = null;
    let camRecorder = null, screenRecorder = null;
    let camBuffers = [], screenBuffers = [];

    function pickMime() {
        if (MediaRecorder.isTypeSupported("video/webm;codecs=vp9")) return { mimeType: "video/webm;codecs=vp9" };
        if (MediaRecorder.isTypeSupported("video/webm;codecs=vp8")) return { mimeType: "video/webm;codecs=vp8" };
        return { mimeType: "video/webm" };
    }

    async function ensureCam() {
        const s = window.mediaGate?.pop?.("camPreview");
        if (s) return s;
        return await navigator.mediaDevices.getUserMedia({ video: true, audio: { echoCancellation: true, noiseSuppression: true } });
    }

    async function ensureScreen() {
        const s = window.mediaGate?.pop?.("screenPreview");
        if (s) return s;
        const str = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: 25 }, audio: false });
        return str;
    }

    async function start(sessId) {
        sessionId = sessId;
        camStream = await ensureCam();
        screenStream = await ensureScreen();

        camBuffers = []; screenBuffers = [];
        const camOpts = pickMime(), scrOpts = pickMime();

        camRecorder = new MediaRecorder(camStream, camOpts);
        screenRecorder = new MediaRecorder(screenStream, scrOpts);

        camRecorder.ondataavailable = e => { if (e.data && e.data.size) camBuffers.push(e.data); };
        screenRecorder.ondataavailable = e => { if (e.data && e.data.size) screenBuffers.push(e.data); };

        camRecorder.onstop = async () => { await uploadOne("cam", camBuffers, camOpts); camBuffers = []; };
        screenRecorder.onstop = async () => { await uploadOne("screen", screenBuffers, scrOpts); screenBuffers = []; };

        // Kullanıcı paylaşımı durdurursa sınav iptal edilsin (proctor'a ilet)
        const scrTrack = screenStream.getVideoTracks()[0];
        if (scrTrack) {
            scrTrack.addEventListener("ended", () => {
                // .NET tarafına politik iptal tetikle
                try { window.examProctor && window.examProctor.enforcePolicies && window.examProctor.enforcePolicies(); } catch { }
                // ayrıca güvenli taraftan bitirmeyi de iste
                try { window.dotnetRunnerCancel && window.dotnetRunnerCancel("Ekran paylaşımı sonlandırıldı"); } catch { }
            });
        }

        camRecorder.start();
        screenRecorder.start();
        console.log("[recorder] dual recording started", sessionId);
    }

    async function uploadOne(kind, buffers, options) {
        try {
            if (!buffers.length) return;
            const blob = new Blob(buffers, options || { type: "video/webm" });
            const res = await fetch(`/api/recordings/finalize-upload?sessionId=${encodeURIComponent(sessionId)}&mime=video/webm&kind=${encodeURIComponent(kind)}`, {
                method: "POST",
                body: blob
            });
            if (!res.ok) console.error("[recorder] finalize-upload failed", kind, res.status);
        } catch (e) { console.error("[recorder] uploadOne", kind, e); }
    }

    function stopTracks(stream) { try { stream?.getTracks()?.forEach(t => { try { t.stop() } catch { } }); } catch { } }

    async function stop() {
        try { if (camRecorder && camRecorder.state !== "inactive") camRecorder.stop(); } catch { }
        try { if (screenRecorder && screenRecorder.state !== "inactive") screenRecorder.stop(); } catch { }
        await new Promise(r => setTimeout(r, 100));
        stopTracks(camStream); stopTracks(screenStream);
        camStream = null; screenStream = null; camRecorder = null; screenRecorder = null; sessionId = null;
    }

    async function cancel() {
        try { if (camRecorder && camRecorder.state !== "inactive") camRecorder.stop(); } catch { }
        try { if (screenRecorder && screenRecorder.state !== "inactive") screenRecorder.stop(); } catch { }
        camBuffers = []; screenBuffers = [];
        stopTracks(camStream); stopTracks(screenStream);
        camStream = null; screenStream = null; camRecorder = null; screenRecorder = null; sessionId = null;
    }

    return { start, stop, cancel };
})();
