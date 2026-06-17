// Sekme değişimi, blur, Alt+Tab, reload/close gibi ihlallerde .NET'e iptal çağrısı yapar.
window.examProctor = (function () {
    let dotnet = null;
    let sessionId = null;
    let canceled = false;
    let initialized = false;
    let initTime = 0;

    const gracePeriodMs = 2500;

    let blurHandler = null;
    let visibilityHandler = null;
    let keydownHandler = null;
    let beforeUnloadHandler = null;
    let pageHideHandler = null;

    function init(dotnetRef, sessId) {
        dispose();

        dotnet = dotnetRef;
        sessionId = sessId;
        canceled = false;
        initialized = true;
        initTime = Date.now();

        enforcePolicies();
    }

    function enforcePolicies() {
        try {
            const nav = performance.getEntriesByType &&
                performance.getEntriesByType("navigation");

            const navigationType = nav && nav[0]
                ? nav[0].type
                : (performance.navigation && performance.navigation.type === 1 ? "reload" : "navigate");

            if (navigationType === "reload") {
                hardCancel("Sayfa yenilendi");
                return;
            }

            blurHandler = function () {
                if (isInGracePeriod()) {
                    return;
                }

                hardCancel("Odak kaybı");
            };

            visibilityHandler = function () {
                if (isInGracePeriod()) {
                    return;
                }

                if (document.visibilityState !== "visible") {
                    hardCancel("Sekme değiştirildi / görünürlük kaybı");
                }
            };

            keydownHandler = function (e) {
                if (e.altKey && (e.key === "Tab" || e.code === "Tab")) {
                    hardCancel("Alt+Tab algılandı");
                }

                if (e.ctrlKey && (e.key === "r" || e.key === "R")) {
                    hardCancel("Sayfa yenileme kısayolu algılandı");
                }

                if (e.key === "F5" || e.code === "F5") {
                    hardCancel("Sayfa yenileme kısayolu algılandı");
                }
            };

            beforeUnloadHandler = function () {
                notifyCancelWithoutAwait("Sayfa kapatıldı / yenilendi");
            };

            pageHideHandler = function () {
                notifyCancelWithoutAwait("Sayfa gizlendi / kapatıldı");
            };

            window.addEventListener("blur", blurHandler);
            document.addEventListener("visibilitychange", visibilityHandler);
            window.addEventListener("keydown", keydownHandler, { capture: true });
            window.addEventListener("beforeunload", beforeUnloadHandler);
            window.addEventListener("pagehide", pageHideHandler);
        } catch (e) {
            console.warn("[examProctor] enforcePolicies error", e);
        }
    }

    function isInGracePeriod() {
        return Date.now() - initTime < gracePeriodMs;
    }

    async function hardCancel(reason) {
        if (canceled) {
            return;
        }

        canceled = true;

        try {
            if (dotnet) {
                await dotnet.invokeMethodAsync("CancelByPolicy", reason || "Policy");
            }
        } catch (e) {
            console.warn("[examProctor] CancelByPolicy failed", e);
        }

        try {
            window.close();
        } catch {
        }
    }

    function notifyCancelWithoutAwait(reason) {
        if (canceled) {
            return;
        }

        canceled = true;

        try {
            if (dotnet) {
                dotnet.invokeMethodAsync("CancelByPolicy", reason || "Policy");
            }
        } catch {
        }
    }

    function dispose() {
        try {
            if (blurHandler) {
                window.removeEventListener("blur", blurHandler);
            }

            if (visibilityHandler) {
                document.removeEventListener("visibilitychange", visibilityHandler);
            }

            if (keydownHandler) {
                window.removeEventListener("keydown", keydownHandler, { capture: true });
            }

            if (beforeUnloadHandler) {
                window.removeEventListener("beforeunload", beforeUnloadHandler);
            }

            if (pageHideHandler) {
                window.removeEventListener("pagehide", pageHideHandler);
            }
        } catch {
        }

        blurHandler = null;
        visibilityHandler = null;
        keydownHandler = null;
        beforeUnloadHandler = null;
        pageHideHandler = null;

        initialized = false;
        dotnet = null;
        sessionId = null;
    }

    return {
        init,
        enforcePolicies,
        hardCancel,
        dispose
    };
})();