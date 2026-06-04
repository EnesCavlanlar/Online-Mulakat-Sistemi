// Aday kamerasını ve cihaz ekranını aynı anda kaydeder.
// Sınav bitince iki ayrı .webm dosyası olarak sunucuya yükler.

window.examRecorder = (function () {
    let sessionId = null;

    let camStream = null;
    let screenStream = null;

    let camRecorder = null;
    let screenRecorder = null;

    let camBuffers = [];
    let screenBuffers = [];

    let camUploadPromise = null;
    let screenUploadPromise = null;

    function pickMime() {
        if (window.MediaRecorder && MediaRecorder.isTypeSupported("video/webm;codecs=vp9")) {
            return { mimeType: "video/webm;codecs=vp9" };
        }

        if (window.MediaRecorder && MediaRecorder.isTypeSupported("video/webm;codecs=vp8")) {
            return { mimeType: "video/webm;codecs=vp8" };
        }

        return { mimeType: "video/webm" };
    }

    async function ensureCam() {
        const previewStream = window.mediaGate?.pop?.("camPreview");

        if (previewStream) {
            return previewStream;
        }

        return await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: {
                echoCancellation: true,
                noiseSuppression: true
            }
        });
    }

    async function ensureScreen() {
        const previewStream = window.mediaGate?.pop?.("screenPreview");

        if (previewStream) {
            return previewStream;
        }

        return await navigator.mediaDevices.getDisplayMedia({
            video: {
                frameRate: 25
            },
            audio: false
        });
    }

    async function start(sessId) {
        if (!sessId) {
            console.error("[recorder] sessionId boş geldi.");
            return;
        }

        if (!window.MediaRecorder) {
            console.error("[recorder] Bu tarayıcı MediaRecorder desteklemiyor.");
            return;
        }

        sessionId = sessId;

        camStream = await ensureCam();
        screenStream = await ensureScreen();

        camBuffers = [];
        screenBuffers = [];

        camUploadPromise = null;
        screenUploadPromise = null;

        const camOptions = pickMime();
        const screenOptions = pickMime();

        camRecorder = new MediaRecorder(camStream, camOptions);
        screenRecorder = new MediaRecorder(screenStream, screenOptions);

        camRecorder.ondataavailable = function (event) {
            if (event.data && event.data.size > 0) {
                camBuffers.push(event.data);
            }
        };

        screenRecorder.ondataavailable = function (event) {
            if (event.data && event.data.size > 0) {
                screenBuffers.push(event.data);
            }
        };

        camRecorder.onstop = function () {
            camUploadPromise = uploadOne("cam", camBuffers, camOptions);
        };

        screenRecorder.onstop = function () {
            screenUploadPromise = uploadOne("screen", screenBuffers, screenOptions);
        };

        const screenTrack = screenStream.getVideoTracks()[0];

        if (screenTrack) {
            screenTrack.addEventListener("ended", function () {
                try {
                    if (window.examProctor && typeof window.examProctor.enforcePolicies === "function") {
                        window.examProctor.enforcePolicies();
                    }
                } catch (e) {
                    console.warn("[recorder] enforcePolicies error", e);
                }

                try {
                    if (window.dotnetRunnerCancel) {
                        window.dotnetRunnerCancel("Ekran paylaşımı sonlandırıldı.");
                    }
                } catch (e) {
                    console.warn("[recorder] dotnetRunnerCancel error", e);
                }
            });
        }

        camRecorder.start(1000);
        screenRecorder.start(1000);

        console.log("[recorder] kayıt başladı:", sessionId);
    }

    async function uploadOne(kind, buffers, options) {
        try {
            if (!sessionId) {
                console.error("[recorder] upload sessionId boş:", kind);
                return;
            }

            if (!buffers || buffers.length === 0) {
                console.warn("[recorder] yüklenecek kayıt yok:", kind);
                return;
            }

            const blob = new Blob(buffers, options || { type: "video/webm" });

            if (blob.size <= 0) {
                console.warn("[recorder] kayıt dosyası boş:", kind);
                return;
            }

            const url =
                "/api/recordings/finalize-upload" +
                "?sessionId=" + encodeURIComponent(sessionId) +
                "&mime=" + encodeURIComponent(blob.type || "video/webm") +
                "&kind=" + encodeURIComponent(kind);

            const response = await fetch(url, {
                method: "POST",
                body: blob
            });

            if (!response.ok) {
                const text = await response.text();
                console.error("[recorder] kayıt yükleme başarısız:", kind, response.status, text);
                return;
            }

            console.log("[recorder] kayıt yüklendi:", kind, blob.size);
        } catch (e) {
            console.error("[recorder] uploadOne hata:", kind, e);
        }
    }

    function stopTracks(stream) {
        try {
            if (!stream) {
                return;
            }

            stream.getTracks().forEach(function (track) {
                try {
                    track.stop();
                } catch (e) {
                    console.warn("[recorder] track stop error", e);
                }
            });
        } catch (e) {
            console.warn("[recorder] stopTracks error", e);
        }
    }

    function requestFinalData(recorder) {
        try {
            if (recorder && recorder.state === "recording") {
                recorder.requestData();
            }
        } catch (e) {
            console.warn("[recorder] requestData error", e);
        }
    }

    function stopRecorder(recorder) {
        try {
            if (recorder && recorder.state !== "inactive") {
                recorder.stop();
            }
        } catch (e) {
            console.warn("[recorder] recorder stop error", e);
        }
    }

    async function stop() {
        const currentSessionId = sessionId;

        try {
            requestFinalData(camRecorder);
            requestFinalData(screenRecorder);

            stopRecorder(camRecorder);
            stopRecorder(screenRecorder);

            await new Promise(function (resolve) {
                setTimeout(resolve, 500);
            });

            if (camUploadPromise) {
                await camUploadPromise;
            }

            if (screenUploadPromise) {
                await screenUploadPromise;
            }

            console.log("[recorder] kayıt durduruldu ve yükleme tamamlandı:", currentSessionId);
        } catch (e) {
            console.error("[recorder] stop error", e);
        } finally {
            stopTracks(camStream);
            stopTracks(screenStream);

            camStream = null;
            screenStream = null;

            camRecorder = null;
            screenRecorder = null;

            camBuffers = [];
            screenBuffers = [];

            camUploadPromise = null;
            screenUploadPromise = null;

            sessionId = null;
        }
    }

    async function cancel() {
        try {
            camBuffers = [];
            screenBuffers = [];

            stopRecorder(camRecorder);
            stopRecorder(screenRecorder);
        } catch (e) {
            console.warn("[recorder] cancel error", e);
        } finally {
            stopTracks(camStream);
            stopTracks(screenStream);

            camStream = null;
            screenStream = null;

            camRecorder = null;
            screenRecorder = null;

            camBuffers = [];
            screenBuffers = [];

            camUploadPromise = null;
            screenUploadPromise = null;

            sessionId = null;
        }
    }

    return {
        start: start,
        stop: stop,
        cancel: cancel
    };
})();