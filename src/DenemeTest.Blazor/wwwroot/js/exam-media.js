// window.mediaGate: Kamera/Mikrofon + Ekran paylaşımı izin/önizleme yöneticisi
window.mediaGate = (function () {
    const previews = new Map(); // elementId -> MediaStream
    const previewInfos = new Map(); // elementId -> info object

    function ensureMediaDevices() {
        if (!navigator.mediaDevices) {
            throw new Error("Tarayıcı medya cihazlarını desteklemiyor.");
        }

        if (typeof navigator.mediaDevices.getUserMedia !== "function") {
            throw new Error("Tarayıcı kamera/mikrofon erişimini desteklemiyor.");
        }

        if (typeof navigator.mediaDevices.getDisplayMedia !== "function") {
            throw new Error("Tarayıcı ekran paylaşımını desteklemiyor.");
        }
    }

    async function requestCam(videoId) {
        ensureMediaDevices();

        if (!videoId) {
            throw new Error("Kamera önizleme elementi bulunamadı.");
        }

        stopPreview(videoId);

        const stream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: {
                echoCancellation: true,
                noiseSuppression: true
            }
        });

        validateLiveStream(stream, "Kamera/mikrofon akışı başlatılamadı.");

        attach(videoId, stream, {
            kind: "cam",
            isFullScreen: true,
            displaySurface: "camera"
        });

        return true;
    }

    async function requestScreen(videoId) {
        ensureMediaDevices();

        if (!videoId) {
            throw new Error("Ekran önizleme elementi bulunamadı.");
        }

        stopPreview(videoId);

        let stream = null;

        try {
            stream = await navigator.mediaDevices.getDisplayMedia({
                video: {
                    frameRate: 25,
                    displaySurface: "monitor"
                },
                audio: false,

                // Chrome destekliyorsa adayın tüm ekran seçmesini kolaylaştırır.
                monitorTypeSurfaces: "include",
                selfBrowserSurface: "exclude",
                surfaceSwitching: "exclude"
            });

            validateLiveStream(stream, "Ekran paylaşımı başlatılamadı.");

            const info = getScreenInfo(stream);

            if (!info.canVerifySurface) {
                stopStream(stream);
                throw new Error("Tarayıcı ekran paylaşımı türünü doğrulayamıyor. Güvenlik nedeniyle sınava giriş engellendi. Lütfen güncel Chrome kullanıp 'Tüm ekran' seçin.");
            }

            if (!info.isFullScreen) {
                stopStream(stream);
                throw new Error("Sınava başlamak için pencere veya sekme değil, 'Tüm ekran' paylaşımı seçmelisin.");
            }

            attach(videoId, stream, {
                kind: "screen",
                isFullScreen: true,
                displaySurface: info.displaySurface
            });

            return true;
        } catch (e) {
            if (stream) {
                stopStream(stream);
            }

            throw e;
        }
    }

    function getScreenInfo(stream) {
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

        return {
            canVerifySurface: !!displaySurface,
            displaySurface: displaySurface || "unknown",
            isFullScreen: displaySurface === "monitor"
        };
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

    function attach(videoId, stream, info) {
        const element = document.getElementById(videoId);

        if (!element) {
            stopStream(stream);
            throw new Error("Önizleme video elementi bulunamadı: " + videoId);
        }

        previews.set(videoId, stream);
        previewInfos.set(videoId, info || {});

        element.srcObject = stream;
        element.muted = true;
        element.playsInline = true;

        try {
            const playPromise = element.play();

            if (playPromise && typeof playPromise.catch === "function") {
                playPromise.catch(function (e) {
                    console.warn("[mediaGate] video play engellendi:", e);
                });
            }
        } catch (e) {
            console.warn("[mediaGate] video play hata:", e);
        }
    }

    // recorder tarafına önizleme stream'ini devretmek için.
    // Önemli: pop edilen stream durdurulmaz, sadece map'ten çıkarılır.
    function pop(videoId) {
        const stream = previews.get(videoId);

        if (stream) {
            previews.delete(videoId);
            previewInfos.delete(videoId);

            const element = document.getElementById(videoId);
            if (element) {
                try {
                    element.pause();
                    element.srcObject = null;
                } catch {
                }
            }
        }

        return stream || null;
    }

    function stopPreview(videoId) {
        try {
            const stream = previews.get(videoId);

            if (stream) {
                stopStream(stream);
                previews.delete(videoId);
                previewInfos.delete(videoId);
            }

            const element = document.getElementById(videoId);
            if (element) {
                try {
                    element.pause();
                    element.srcObject = null;
                } catch {
                }
            }
        } catch (e) {
            console.warn("[mediaGate] stopPreview error:", e);
        }
    }

    function stopAllPreviews() {
        try {
            Array.from(previews.keys()).forEach(function (videoId) {
                stopPreview(videoId);
            });
        } catch (e) {
            console.warn("[mediaGate] stopAllPreviews error:", e);
        }
    }

    function hasPreview(videoId) {
        const stream = previews.get(videoId);

        if (!stream || !stream.getTracks) {
            return false;
        }

        return stream.getTracks().some(function (track) {
            return track.readyState === "live";
        });
    }

    function isFullScreenShare(videoId) {
        const info = previewInfos.get(videoId);

        if (!info) {
            return false;
        }

        return info.kind === "screen" && info.isFullScreen === true;
    }

    function getPreviewInfo(videoId) {
        const info = previewInfos.get(videoId);

        if (!info) {
            return {
                exists: false,
                kind: null,
                isFullScreen: false,
                displaySurface: null
            };
        }

        return {
            exists: true,
            kind: info.kind || null,
            isFullScreen: info.isFullScreen === true,
            displaySurface: info.displaySurface || null
        };
    }

    function stopStream(stream) {
        try {
            if (!stream) {
                return;
            }

            stream.getTracks().forEach(function (track) {
                try {
                    track.stop();
                } catch {
                }
            });
        } catch (e) {
            console.warn("[mediaGate] stopStream error:", e);
        }
    }

    return {
        requestCam,
        requestScreen,
        pop,
        stopPreview,
        stopAllPreviews,
        hasPreview,
        isFullScreenShare,
        getPreviewInfo
    };
})();