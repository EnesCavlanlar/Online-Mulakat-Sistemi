// Aday kamerasını ve cihaz ekranını aynı anda kaydeder.
// Sınav bitince veya politika ihlaliyle iptal edilince iki ayrı .webm dosyası olarak sunucuya yükler.

window.examRecorder = (function () {
    let sessionId = null;

    let camStream = null;
    let screenStream = null;

    let camRecorder = null;
    let screenRecorder = null;

    let camBuffers = [];
    let screenBuffers = [];

    let started = false;
    let stopping = false;
    let intentionalStop = false;

    function pickMime() {
        if (window.MediaRecorder && MediaRecorder.isTypeSupported("video/webm;codecs=vp9")) {
            return { mimeType: "video/webm;codecs=vp9" };
        }

        if (window.MediaRecorder && MediaRecorder.isTypeSupported("video/webm;codecs=vp8")) {
            return { mimeType: "video/webm;codecs=vp8" };
        }

        if (window.MediaRecorder && MediaRecorder.isTypeSupported("video/webm")) {
            return { mimeType: "video/webm" };
        }

        return {};
    }

    async function ensureCam() {
        const previewStream = window.mediaGate?.pop?.("camPreview");

        if (previewStream) {
            validateLiveStream(previewStream, "Kamera/mikrofon önizleme akışı aktif değil.");
            return previewStream;
        }

        const stream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: {
                echoCancellation: true,
                noiseSuppression: true
            }
        });

        validateLiveStream(stream, "Kamera/mikrofon akışı başlatılamadı.");
        return stream;
    }

    async function ensureScreen() {
        const previewStream = window.mediaGate?.pop?.("screenPreview");

        if (previewStream) {
            validateLiveStream(previewStream, "Ekran paylaşımı önizleme akışı aktif değil.");
            validateFullScreenShare(previewStream);
            return previewStream;
        }

        const stream = await navigator.mediaDevices.getDisplayMedia({
            video: {
                frameRate: 25,
                displaySurface: "monitor"
            },
            audio: false,
            monitorTypeSurfaces: "include",
            selfBrowserSurface: "exclude",
            surfaceSwitching: "exclude"
        });

        validateLiveStream(stream, "Ekran paylaşımı başlatılamadı.");
        validateFullScreenShare(stream);

        return stream;
    }

    async function start(sessId) {
        if (!sessId) {
            console.error("[recorder] sessionId boş geldi.");
            return false;
        }

        if (!navigator.mediaDevices) {
            console.error("[recorder] navigator.mediaDevices desteklenmiyor.");
            return false;
        }

        if (!window.MediaRecorder) {
            console.error("[recorder] Bu tarayıcı MediaRecorder desteklemiyor.");
            return false;
        }

        if (started && sessionId === sessId) {
            console.warn("[recorder] kayıt zaten başlamış:", sessId);
            return true;
        }

        if (started) {
            await stop();
        }

        try {
            sessionId = sessId;

            started = false;
            stopping = false;
            intentionalStop = false;

            camBuffers = [];
            screenBuffers = [];

            camStream = await ensureCam();
            screenStream = await ensureScreen();

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

            camRecorder.onerror = function (event) {
                console.error("[recorder] kamera recorder hatası:", event);
            };

            screenRecorder.onerror = function (event) {
                console.error("[recorder] ekran recorder hatası:", event);
            };

            const screenTrack = screenStream.getVideoTracks()[0];

            if (screenTrack) {
                screenTrack.addEventListener("ended", function () {
                    if (intentionalStop || stopping) {
                        return;
                    }

                    console.warn("[recorder] ekran paylaşımı aday tarafından sonlandırıldı.");

                    try {
                        if (window.examProctor && typeof window.examProctor.hardCancel === "function") {
                            window.examProctor.hardCancel("Ekran paylaşımı sonlandırıldı.");
                        }
                    } catch (e) {
                        console.warn("[recorder] examProctor hardCancel error", e);
                    }
                });
            }

            camRecorder.start(1000);
            screenRecorder.start(1000);

            started = true;

            console.log("[recorder] kayıt başladı:", sessionId);
            return true;
        } catch (e) {
            console.error("[recorder] start error", e);

            cleanupStreamsAndState(false);
            return false;
        }
    }

    async function stop() {
        if (stopping) {
            return;
        }

        const currentSessionId = sessionId;

        if (!currentSessionId) {
            cleanupStreamsAndState(false);
            return;
        }

        stopping = true;
        intentionalStop = true;

        try {
            await requestFinalDataSafely(camRecorder);
            await requestFinalDataSafely(screenRecorder);

            await Promise.all([
                stopRecorderAndWait(camRecorder),
                stopRecorderAndWait(screenRecorder)
            ]);

            // Stop sonrası final chunk'ın buffer'a düşmesi için kısa bekleme.
            await delay(500);

            const camUploadOk = await uploadOne(currentSessionId, "cam", camBuffers, camRecorder);
            const screenUploadOk = await uploadOne(currentSessionId, "screen", screenBuffers, screenRecorder);

            console.log("[recorder] kayıt durduruldu:", currentSessionId, {
                camUploadOk,
                screenUploadOk,
                camChunks: camBuffers.length,
                screenChunks: screenBuffers.length
            });
        } catch (e) {
            console.error("[recorder] stop error", e);
        } finally {
            cleanupStreamsAndState(false);
        }
    }

    async function cancel() {
        try {
            stopping = true;
            intentionalStop = true;

            camBuffers = [];
            screenBuffers = [];

            await Promise.all([
                stopRecorderAndWait(camRecorder),
                stopRecorderAndWait(screenRecorder)
            ]);

            console.log("[recorder] kayıt iptal edildi, dosyalar yüklenmedi:", sessionId);
        } catch (e) {
            console.warn("[recorder] cancel error", e);
        } finally {
            cleanupStreamsAndState(false);
        }
    }

    async function requestFinalDataSafely(recorder) {
        try {
            if (recorder && recorder.state === "recording") {
                recorder.requestData();
            }
        } catch (e) {
            console.warn("[recorder] requestData error", e);
        }

        await delay(250);
    }

    function stopRecorderAndWait(recorder) {
        return new Promise(function (resolve) {
            try {
                if (!recorder || recorder.state === "inactive") {
                    resolve();
                    return;
                }

                let resolved = false;

                const done = function () {
                    if (resolved) {
                        return;
                    }

                    resolved = true;
                    resolve();
                };

                recorder.addEventListener("stop", done, { once: true });
                recorder.stop();

                // Bazı tarayıcılarda stop eventi geç gelebilir, akışı kilitlemeyelim.
                setTimeout(done, 2500);
            } catch (e) {
                console.warn("[recorder] recorder stop error", e);
                resolve();
            }
        });
    }

    async function uploadOne(uploadSessionId, kind, buffers, recorder) {
        try {
            if (!uploadSessionId) {
                console.error("[recorder] upload sessionId boş:", kind);
                return false;
            }

            if (!buffers || buffers.length === 0) {
                console.warn("[recorder] yüklenecek kayıt yok:", kind);
                return false;
            }

            const mimeType =
                recorder?.mimeType ||
                "video/webm";

            const blob = new Blob(buffers, {
                type: mimeType
            });

            if (blob.size <= 0) {
                console.warn("[recorder] kayıt dosyası boş:", kind);
                return false;
            }

            const url =
                "/api/recordings/finalize-upload" +
                "?sessionId=" + encodeURIComponent(uploadSessionId) +
                "&mime=" + encodeURIComponent(blob.type || "video/webm") +
                "&kind=" + encodeURIComponent(kind) +
                "&t=" + encodeURIComponent(Date.now().toString());

            console.log("[recorder] kayıt yükleniyor:", kind, blob.size, uploadSessionId);

            const response = await fetch(url, {
                method: "POST",
                body: blob,
                cache: "no-store"
            });

            if (!response.ok) {
                const text = await response.text();
                console.error("[recorder] kayıt yükleme başarısız:", kind, response.status, text);
                return false;
            }

            const resultText = await response.text();
            console.log("[recorder] kayıt yüklendi:", kind, blob.size, resultText);
            return true;
        } catch (e) {
            console.error("[recorder] uploadOne hata:", kind, e);
            return false;
        }
    }

    function validateLiveStream(stream, message) {
        if (!stream || !stream.getTracks) {
            throw new Error(message);
        }

        const hasLiveTrack = stream
            .getTracks()
            .some(function (track) {
                return track.readyState === "live";
            });

        if (!hasLiveTrack) {
            throw new Error(message);
        }
    }

    function validateFullScreenShare(stream) {
        const videoTrack = stream && stream.getVideoTracks
            ? stream.getVideoTracks()[0]
            : null;

        const settings = videoTrack && videoTrack.getSettings
            ? videoTrack.getSettings()
            : null;

        const rawDisplaySurface = settings &&
            (settings.displaySurface || settings.displaySurfaceType);

        const displaySurface = rawDisplaySurface
            ? String(rawDisplaySurface).toLowerCase()
            : "";

        if (!displaySurface) {
            stopTracks(stream);
            throw new Error("Tarayıcı ekran paylaşımı türünü doğrulayamıyor. Lütfen güncel Chrome kullanıp 'Tüm ekran' seçin.");
        }

        if (displaySurface !== "monitor") {
            stopTracks(stream);
            throw new Error("Sınava devam etmek için pencere veya sekme değil, 'Tüm ekran' paylaşımı seçmelisin.");
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

    function cleanupStreamsAndState(keepSession) {
        stopTracks(camStream);
        stopTracks(screenStream);

        camStream = null;
        screenStream = null;

        camRecorder = null;
        screenRecorder = null;

        camBuffers = [];
        screenBuffers = [];

        started = false;
        stopping = false;
        intentionalStop = false;

        if (!keepSession) {
            sessionId = null;
        }
    }

    function delay(ms) {
        return new Promise(function (resolve) {
            setTimeout(resolve, ms);
        });
    }

    function getStatus() {
        return {
            sessionId,
            started,
            stopping,
            camChunks: camBuffers.length,
            screenChunks: screenBuffers.length,
            camRecorderState: camRecorder ? camRecorder.state : null,
            screenRecorderState: screenRecorder ? screenRecorder.state : null
        };
    }

    return {
        start: start,
        stop: stop,
        cancel: cancel,
        getStatus: getStatus
    };
})();