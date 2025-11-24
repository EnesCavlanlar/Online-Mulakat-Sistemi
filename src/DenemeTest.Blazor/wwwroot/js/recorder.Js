// wwwroot/js/recorder.js
// Ekran + kamera + ses -> MediaRecorder -> TUM BLOBLARI TARAYICIDA BIRIKTIR
// Stop'ta TEK webm dosyası olarak sunucuya yükle (finalize-upload)

window.examRecorder = (function () {
    let mediaStream = null;
    let mediaRecorder = null;
    let sessionId = null;
    let buffers = [];
    let options = {};
    let recording = false;

    async function start(sessId, /*timesliceMs*/ _ignored, withScreen) {
        try {
            if (recording && sessionId === sessId && mediaRecorder && mediaRecorder.state === "recording") {
                console.log("[recorder] already recording", sessId);
                return;
            }

            await stopStreams();
            sessionId = sessId;
            buffers = [];
            recording = false;

            const tracks = [];
            // Ekran
            if (withScreen && navigator.mediaDevices.getDisplayMedia) {
                try {
                    const screen = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: 25 }, audio: false });
                    screen.getVideoTracks().forEach(t => tracks.push(t));
                    console.log("[recorder] display media OK");
                } catch (e) {
                    console.warn("[recorder] display media failed, continue with camera only", e);
                }
            }
            // Kamera + ses
            const cam = await navigator.mediaDevices.getUserMedia({
                video: true,
                audio: { echoCancellation: true, noiseSuppression: true }
            });
            cam.getTracks().forEach(t => tracks.push(t));

            mediaStream = new MediaStream(tracks);

            options = {};
            if (MediaRecorder.isTypeSupported("video/webm;codecs=vp9")) options.mimeType = "video/webm;codecs=vp9";
            else if (MediaRecorder.isTypeSupported("video/webm;codecs=vp8")) options.mimeType = "video/webm;codecs=vp8";
            else options.mimeType = "video/webm";

            mediaRecorder = new MediaRecorder(mediaStream, options);

            mediaRecorder.ondataavailable = (e) => {
                if (e && e.data && e.data.size > 0) {
                    buffers.push(e.data);
                }
            };

            mediaRecorder.onstart = () => {
                recording = true;
                console.log("[recorder] started", sessionId);
            };

            mediaRecorder.onerror = (e) => {
                console.error("[recorder] error", e);
            };

            mediaRecorder.onstop = async () => {
                try {
                    const blob = new Blob(buffers, options);
                    buffers = [];

                    // Tek webm dosyasını bir seferde yükle
                    const res = await fetch(`/api/recordings/finalize-upload?sessionId=${encodeURIComponent(sessionId)}&mime=video/webm`, {
                        method: "POST",
                        body: blob
                    });
                    if (!res.ok) {
                        console.error("[recorder] finalize-upload failed", res.status);
                    } else {
                        console.log("[recorder] finalize-upload OK");
                    }
                } catch (err) {
                    console.error("[recorder] finalize-upload error", err);
                } finally {
                    await stopStreams();
                    mediaRecorder = null;
                    sessionId = null;
                    recording = false;
                }
            };

            // DİKKAT: timeslice kullanmıyoruz -> valid tek webm dosyası oluşur
            mediaRecorder.start();
        } catch (err) {
            console.error("[recorder] start error", err);
        }
    }

    async function stop(sessId) {
        try {
            if (mediaRecorder && mediaRecorder.state !== "inactive") {
                mediaRecorder.stop();
            } else {
                // kayıt hiç başlamadıysa bile finalize-upload boş gönderme
                await stopStreams();
                mediaRecorder = null;
                sessionId = null;
                recording = false;
            }
        } catch (e) {
            console.warn("[recorder] stop error", e);
        }
    }

    async function cancel(sessId) {
        try {
            if (mediaRecorder && mediaRecorder.state !== "inactive") {
                mediaRecorder.stop();
            }
        } catch (e) {
            console.warn("[recorder] stop on cancel error", e);
        } finally {
            // Sunucu tarafında geçici parçaları silmeye gerek yok (chunk yok artık)
            await stopStreams();
            mediaRecorder = null;
            sessionId = null;
            recording = false;
            buffers = [];
        }
    }

    async function stopStreams() {
        if (mediaStream) {
            mediaStream.getTracks().forEach(t => { try { t.stop(); } catch { } });
            mediaStream = null;
        }
    }

    return { start, stop, cancel };
})();
