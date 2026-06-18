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

    let camBytes = 0;
    let screenBytes = 0;

    let started = false;
    let stopping = false;
    let intentionalStop = false;

    const RECORDER_TIMESLICE_MS = 1000;
    const MIN_UPLOAD_BYTES = 1024;

    function pickMimeForStream(stream, label) {
        const hasAudio = !!(stream && stream.getAudioTracks && stream.getAudioTracks().some(t => t.readyState === "live"));

        const candidates = hasAudio
            ? [
                "video/webm;codecs=vp8,opus",
                "video/webm;codecs=vp8",
                "video/webm"
            ]
            : [
                "video/webm;codecs=vp8",
                "video/webm"
            ];

        for (const mime of candidates) {
            try {
                if (window.MediaRecorder && MediaRecorder.isTypeSupported(mime)) {
                    console.log("[recorder] mime seçildi:", label, mime);
                    return { mimeType: mime };
                }
            } catch (e) {
                console.warn("[recorder] mime kontrol hatası:", label, mime, e);
            }
        }

        console.warn("[recorder] desteklenen özel mime bulunamadı, default kullanılacak:", label);
        return {};
    }

    async function ensureCam() {
        const previewStream = window.mediaGate?.pop?.("camPreview");

        if (previewStream) {
            validateLiveStream(previewStream, "Kamera/mikrofon önizleme akışı aktif değil.");
            validateCameraStream(previewStream);
            return previewStream;
        }

        const stream = await navigator.mediaDevices.getUserMedia({
            video: {
                width: { ideal: 1280 },
                height: { ideal: 720 },
                frameRate: { ideal: 24, max: 30 }
            },
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            }
        });

        validateLiveStream(stream, "Kamera/mikrofon akışı başlatılamadı.");
        validateCameraStream(stream);

        return stream;
    }

    async function ensureScreen() {
        const previewStream = window.mediaGate?.pop?.("screenPreview");

        if (previewStream) {
            validateLiveStream(previewStream, "Ekran paylaşımı önizleme akışı aktif değil.");
            validateScreenStream(previewStream);
            validateFullScreenShare(previewStream);
            return previewStream;
        }

        const stream = await navigator.mediaDevices.getDisplayMedia({
            video: {
                frameRate: { ideal: 15, max: 25 },
                displaySurface: "monitor"
            },
            audio: false,
            monitorTypeSurfaces: "include",
            selfBrowserSurface: "exclude",
            surfaceSwitching: "exclude"
        });

        validateLiveStream(stream, "Ekran paylaşımı başlatılamadı.");
        validateScreenStream(stream);
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
            camBytes = 0;
            screenBytes = 0;

            camStream = await ensureCam();
            screenStream = await ensureScreen();

            logStreamInfo("cam", camStream);
            logStreamInfo("screen", screenStream);

            const camOptions = pickMimeForStream(camStream, "cam");
            const screenOptions = pickMimeForStream(screenStream, "screen");

            camRecorder = new MediaRecorder(camStream, camOptions);
            screenRecorder = new MediaRecorder(screenStream, screenOptions);

            attachRecorderHandlers(camRecorder, "cam", camBuffers, function (size) {
                camBytes += size;
            });

            attachRecorderHandlers(screenRecorder, "screen", screenBuffers, function (size) {
                screenBytes += size;
            });

            attachTrackEndHandlers();

            camRecorder.start(RECORDER_TIMESLICE_MS);
            screenRecorder.start(RECORDER_TIMESLICE_MS);

            await delay(250);

            if (camRecorder.state !== "recording") {
                throw new Error("Kamera recorder recording state'e geçemedi. State: " + camRecorder.state);
            }

            if (screenRecorder.state !== "recording") {
                throw new Error("Ekran recorder recording state'e geçemedi. State: " + screenRecorder.state);
            }

            started = true;

            console.log("[recorder] kayıt başladı:", sessionId, getStatus());
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

        const localCamRecorder = camRecorder;
        const localScreenRecorder = screenRecorder;

        try {
            console.log("[recorder] stop başladı:", currentSessionId, getStatus());

            await requestFinalDataSafely(localCamRecorder, "cam");
            await requestFinalDataSafely(localScreenRecorder, "screen");

            await Promise.all([
                stopRecorderAndWait(localCamRecorder, "cam"),
                stopRecorderAndWait(localScreenRecorder, "screen")
            ]);

            await delay(1000);

            console.log("[recorder] stop sonrası buffer durumu:", {
                sessionId: currentSessionId,
                camChunks: camBuffers.length,
                camBytes,
                screenChunks: screenBuffers.length,
                screenBytes,
                camState: localCamRecorder ? localCamRecorder.state : null,
                screenState: localScreenRecorder ? localScreenRecorder.state : null
            });

            const camUploadOk = await uploadOne(currentSessionId, "cam", camBuffers, localCamRecorder);
            const screenUploadOk = await uploadOne(currentSessionId, "screen", screenBuffers, localScreenRecorder);

            console.log("[recorder] kayıt durduruldu:", currentSessionId, {
                camUploadOk,
                screenUploadOk,
                camChunks: camBuffers.length,
                camBytes,
                screenChunks: screenBuffers.length,
                screenBytes
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
            camBytes = 0;
            screenBytes = 0;

            await Promise.all([
                stopRecorderAndWait(camRecorder, "cam"),
                stopRecorderAndWait(screenRecorder, "screen")
            ]);

            console.log("[recorder] kayıt iptal edildi, dosyalar yüklenmedi:", sessionId);
        } catch (e) {
            console.warn("[recorder] cancel error", e);
        } finally {
            cleanupStreamsAndState(false);
        }
    }

    function attachRecorderHandlers(recorder, kind, buffers, addBytes) {
        recorder.onstart = function () {
            console.log("[recorder] recorder start:", kind, recorder.mimeType, recorder.state);
        };

        recorder.ondataavailable = function (event) {
            const size = event && event.data ? event.data.size : 0;

            if (event.data && size > 0) {
                buffers.push(event.data);
                addBytes(size);

                console.log("[recorder] chunk:", kind, {
                    size,
                    chunks: buffers.length
                });
            } else {
                console.warn("[recorder] boş chunk geldi:", kind, event);
            }
        };

        recorder.onerror = function (event) {
            console.error("[recorder] recorder hatası:", kind, event);
        };

        recorder.onpause = function () {
            console.warn("[recorder] recorder pause:", kind);
        };

        recorder.onresume = function () {
            console.log("[recorder] recorder resume:", kind);
        };

        recorder.onstop = function () {
            console.log("[recorder] recorder stop event:", kind, {
                chunks: buffers.length,
                state: recorder.state
            });
        };
    }

    function attachTrackEndHandlers() {
        const screenTrack = screenStream && screenStream.getVideoTracks
            ? screenStream.getVideoTracks()[0]
            : null;

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

        const camTrack = camStream && camStream.getVideoTracks
            ? camStream.getVideoTracks()[0]
            : null;

        if (camTrack) {
            camTrack.addEventListener("ended", function () {
                if (intentionalStop || stopping) {
                    return;
                }

                console.warn("[recorder] kamera video track sonlandı.");

                try {
                    if (window.examProctor && typeof window.examProctor.hardCancel === "function") {
                        window.examProctor.hardCancel("Kamera görüntüsü sonlandırıldı.");
                    }
                } catch (e) {
                    console.warn("[recorder] examProctor hardCancel error", e);
                }
            });
        }
    }

    async function requestFinalDataSafely(recorder, kind) {
        try {
            if (recorder && recorder.state === "recording") {
                recorder.requestData();
                console.log("[recorder] requestData:", kind);
            }
        } catch (e) {
            console.warn("[recorder] requestData error:", kind, e);
        }

        await delay(500);
    }

    function stopRecorderAndWait(recorder, kind) {
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
                    console.log("[recorder] stop wait tamam:", kind);
                    resolve();
                };

                recorder.addEventListener("stop", done, { once: true });
                recorder.stop();

                setTimeout(done, 5000);
            } catch (e) {
                console.warn("[recorder] recorder stop error:", kind, e);
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
                type: mimeType || "video/webm"
            });

            if (blob.size < MIN_UPLOAD_BYTES) {
                console.warn("[recorder] kayıt dosyası çok küçük veya boş:", kind, blob.size);
                return false;
            }

            const url =
                "/api/recordings/finalize-upload" +
                "?sessionId=" + encodeURIComponent(uploadSessionId) +
                "&mime=" + encodeURIComponent(blob.type || "video/webm") +
                "&kind=" + encodeURIComponent(kind) +
                "&t=" + encodeURIComponent(Date.now().toString());

            console.log("[recorder] kayıt yükleniyor:", kind, {
                size: blob.size,
                type: blob.type,
                sessionId: uploadSessionId,
                chunks: buffers.length
            });

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

    function validateCameraStream(stream) {
        const videoTracks = stream && stream.getVideoTracks
            ? stream.getVideoTracks()
            : [];

        if (!videoTracks || videoTracks.length === 0) {
            stopTracks(stream);
            throw new Error("Kamera video track bulunamadı. Sadece mikrofon izni yeterli değildir.");
        }

        const liveVideo = videoTracks.some(function (track) {
            return track.readyState === "live";
        });

        if (!liveVideo) {
            stopTracks(stream);
            throw new Error("Kamera video track aktif değil.");
        }
    }

    function validateScreenStream(stream) {
        const videoTracks = stream && stream.getVideoTracks
            ? stream.getVideoTracks()
            : [];

        if (!videoTracks || videoTracks.length === 0) {
            stopTracks(stream);
            throw new Error("Ekran video track bulunamadı.");
        }

        const liveVideo = videoTracks.some(function (track) {
            return track.readyState === "live";
        });

        if (!liveVideo) {
            stopTracks(stream);
            throw new Error("Ekran video track aktif değil.");
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

    function logStreamInfo(label, stream) {
        try {
            const tracks = stream && stream.getTracks ? stream.getTracks() : [];

            console.log("[recorder] stream info:", label, tracks.map(function (track) {
                const settings = track.getSettings ? track.getSettings() : {};
                return {
                    kind: track.kind,
                    readyState: track.readyState,
                    enabled: track.enabled,
                    muted: track.muted,
                    label: track.label,
                    settings: settings
                };
            }));
        } catch (e) {
            console.warn("[recorder] stream info error:", label, e);
        }
    }

    function getTrackState(stream, kind) {
        try {
            if (!stream) {
                return "no-stream";
            }

            const tracks = kind === "audio"
                ? stream.getAudioTracks()
                : stream.getVideoTracks();

            if (!tracks || tracks.length === 0) {
                return "no-track";
            }

            return tracks
                .map(function (track) {
                    return track.readyState + (track.enabled ? ":enabled" : ":disabled");
                })
                .join(",");
        } catch {
            return "unknown";
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

        camBytes = 0;
        screenBytes = 0;

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

            camBytes,
            screenBytes,

            camRecorderState: camRecorder ? camRecorder.state : null,
            screenRecorderState: screenRecorder ? screenRecorder.state : null,

            camVideoTrackState: getTrackState(camStream, "video"),
            camAudioTrackState: getTrackState(camStream, "audio"),
            screenVideoTrackState: getTrackState(screenStream, "video"),

            camMimeType: camRecorder ? camRecorder.mimeType : null,
            screenMimeType: screenRecorder ? screenRecorder.mimeType : null
        };
    }

    return {
        start: start,
        stop: stop,
        cancel: cancel,
        getStatus: getStatus
    };
})();