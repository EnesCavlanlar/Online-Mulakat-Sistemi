window.examProctor = (function () {
    let dotnet = null;
    let sessionId = null;

    function visibilityHandler() {
        if (document.visibilityState === 'hidden') {
            dotnet.invokeMethodAsync('ReportFocusLost', sessionId, 'visibility hidden');
        }
    }

    function blurHandler() {
        dotnet.invokeMethodAsync('ReportFocusLost', sessionId, 'window blur');
    }

    function beforeUnloadHandler(e) {
        dotnet.invokeMethodAsync('ReportFocusLost', sessionId, 'beforeunload');
    }

    return {
        init: function (dotnetRef, sessId) {
            dotnet = dotnetRef;
            sessionId = sessId;
            document.addEventListener('visibilitychange', visibilityHandler);
            window.addEventListener('blur', blurHandler);
            window.addEventListener('beforeunload', beforeUnloadHandler);
        },
        dispose: function () {
            document.removeEventListener('visibilitychange', visibilityHandler);
            window.removeEventListener('blur', blurHandler);
            window.removeEventListener('beforeunload', beforeUnloadHandler);
            window.mediaGate = (function () {
                let stream = null;
                let videoEl = null;

                async function request(constraints) {
                    if (stream) return true;
                    try {
                        stream = await navigator.mediaDevices.getUserMedia(constraints || { video: true, audio: true });
                        return true;
                    } catch (e) {
                        console.warn("getUserMedia denied", e);
                        return false;
                    }
                }

                function attachPreview(selector) {
                    if (!stream) return false;
                    videoEl = document.querySelector(selector);
                    if (!videoEl) return false;
                    videoEl.srcObject = stream;
                    videoEl.muted = true;
                    videoEl.playsInline = true;
                    videoEl.autoplay = true;
                    const p = videoEl.play();
                    if (p && p.catch) p.catch(() => { });
                    return true;
                }

                function stop() {
                    if (stream) {
                        stream.getTracks().forEach(t => t.stop());
                        stream = null;
                    }
                    if (videoEl) {
                        videoEl.srcObject = null;
                        videoEl = null;
                    }
                }

                return { request, attachPreview, stop };
            })();

        }
    };
})();
