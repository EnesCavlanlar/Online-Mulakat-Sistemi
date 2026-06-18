// Sınav sırasında sekme değişimi, Alt+Tab, odak kaybı, reload/close gibi ihlallerde .NET'e iptal çağrısı yapar.
// Monaco Editor ile çakışmaması için normal harf/sayı tuşlarına kesinlikle müdahale etmez.
window.examProctor = (function () {
    let dotnet = null;
    let sessionId = null;

    let canceled = false;
    let initialized = false;
    let initTime = 0;

    let blurHandler = null;
    let visibilityHandler = null;
    let keydownHandler = null;
    let beforeUnloadHandler = null;
    let pageHideHandler = null;

    let blurTimer = null;

    const gracePeriodMs = 2500;
    const blurConfirmDelayMs = 900;

    function init(dotnetRef, sessId) {
        dispose();

        dotnet = dotnetRef;
        sessionId = sessId ? String(sessId) : null;

        canceled = false;
        initialized = true;
        initTime = Date.now();

        console.log("[examProctor] init", {
            sessionId: sessionId,
            visibilityState: document.visibilityState,
            hasFocus: safeHasFocus()
        });

        enforcePolicies();
    }

    function enforcePolicies() {
        try {
            const navigationType = getNavigationType();

            if (navigationType === "reload") {
                notifyCancelWithoutAwait("Sayfa yenilendi");
                return;
            }

            visibilityHandler = function () {
                if (canceled || !initialized) {
                    return;
                }

                // Sekme değişimi kesin ihlal. Burada grace period kullanmıyoruz.
                if (document.hidden === true || document.visibilityState !== "visible") {
                    hardCancel("Sekme değiştirildi / görünürlük kaybı");
                }
            };

            blurHandler = function () {
                if (shouldIgnoreBlurEvent()) {
                    return;
                }

                clearBlurTimer();

                blurTimer = setTimeout(function () {
                    if (shouldIgnoreBlurEvent()) {
                        return;
                    }

                    if (document.hidden === true || document.visibilityState !== "visible") {
                        hardCancel("Sekme değiştirildi / görünürlük kaybı");
                        return;
                    }

                    if (!safeHasFocus()) {
                        hardCancel("Odak kaybı / pencere değiştirildi");
                    }
                }, blurConfirmDelayMs);
            };

            keydownHandler = function (e) {
                if (canceled || !initialized) {
                    return;
                }

                const key = (e.key || "").toLowerCase();
                const code = (e.code || "").toLowerCase();

                // Normal harf/sayı/yazı yazma olaylarına ASLA müdahale etme.
                // Sadece net yasaklı browser/sistem kısayollarını yakala.

                const isRefresh =
                    (e.ctrlKey && !e.shiftKey && !e.altKey && (key === "r" || code === "keyr")) ||
                    key === "f5" ||
                    code === "f5";

                if (isRefresh) {
                    preventEvent(e);
                    hardCancel("Sayfa yenileme kısayolu algılandı");
                    return;
                }

                const isDevTools =
                    key === "f12" ||
                    code === "f12" ||
                    (e.ctrlKey && e.shiftKey && (key === "i" || key === "j" || key === "c"));

                if (isDevTools) {
                    preventEvent(e);
                    hardCancel("Geliştirici araçları kısayolu algılandı");
                    return;
                }

                // Alt+Tab çoğu tarayıcıda JS'e düşmez, blur ile yakalanır.
                // Düşerse yakalayalım ama normal AltGr kullanımını bozmayalım.
                const isAltTab =
                    e.altKey &&
                    !e.ctrlKey &&
                    (key === "tab" || code === "tab");

                if (isAltTab) {
                    preventEvent(e);
                    hardCancel("Alt+Tab algılandı");
                    return;
                }
            };

            beforeUnloadHandler = function () {
                notifyCancelWithoutAwait("Sayfa kapatıldı / yenilendi");
            };

            pageHideHandler = function () {
                notifyCancelWithoutAwait("Sayfa gizlendi / kapatıldı");
            };

            document.addEventListener("visibilitychange", visibilityHandler, true);
            window.addEventListener("blur", blurHandler, true);

            // Monaco Editor ile çakışmaması için keydown sadece window üzerinde ve bubble phase'te.
            // document capture kullanmıyoruz.
            window.addEventListener("keydown", keydownHandler, false);

            window.addEventListener("beforeunload", beforeUnloadHandler, true);
            window.addEventListener("pagehide", pageHideHandler, true);

            console.log("[examProctor] policies enforced");
        } catch (e) {
            console.warn("[examProctor] enforcePolicies error", e);
        }
    }

    function clearBlurTimer() {
        if (blurTimer) {
            clearTimeout(blurTimer);
            blurTimer = null;
        }
    }

    function getNavigationType() {
        try {
            const nav = performance.getEntriesByType &&
                performance.getEntriesByType("navigation");

            if (nav && nav[0] && nav[0].type) {
                return nav[0].type;
            }

            if (performance.navigation && performance.navigation.type === 1) {
                return "reload";
            }
        } catch {
        }

        return "navigate";
    }

    function safeHasFocus() {
        try {
            if (typeof document.hasFocus === "function") {
                return document.hasFocus();
            }
        } catch {
        }

        return true;
    }

    function isInGracePeriod() {
        return Date.now() - initTime < gracePeriodMs;
    }

    function shouldIgnoreBlurEvent() {
        return canceled || !initialized || isInGracePeriod();
    }

    function preventEvent(e) {
        try {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
        } catch {
        }
    }

    async function hardCancel(reason) {
        if (canceled) {
            return;
        }

        canceled = true;
        clearBlurTimer();

        const finalReason = reason || "Policy";

        console.warn("[examProctor] hardCancel", {
            sessionId: sessionId,
            reason: finalReason
        });

        try {
            if (dotnet) {
                await dotnet.invokeMethodAsync("CancelByPolicy", finalReason);
            } else {
                console.warn("[examProctor] dotnet ref yok, cancel bildirilemedi");
            }
        } catch (e) {
            console.warn("[examProctor] CancelByPolicy failed", e);
        }

        // Pencereyi burada kapatmıyoruz.
        // Runner.razor CancelInternal içinde cevap kaydı, puan hesaplama, recorder.stop/upload ve kapanışı yönetiyor.
    }

    function notifyCancelWithoutAwait(reason) {
        if (canceled) {
            return;
        }

        canceled = true;
        clearBlurTimer();

        const finalReason = reason || "Policy";

        console.warn("[examProctor] notifyCancelWithoutAwait", {
            sessionId: sessionId,
            reason: finalReason
        });

        try {
            if (dotnet) {
                dotnet.invokeMethodAsync("CancelByPolicy", finalReason);
            }
        } catch {
        }
    }

    function dispose() {
        try {
            clearBlurTimer();

            if (visibilityHandler) {
                document.removeEventListener("visibilitychange", visibilityHandler, true);
            }

            if (blurHandler) {
                window.removeEventListener("blur", blurHandler, true);
            }

            if (keydownHandler) {
                window.removeEventListener("keydown", keydownHandler, false);
            }

            if (beforeUnloadHandler) {
                window.removeEventListener("beforeunload", beforeUnloadHandler, true);
            }

            if (pageHideHandler) {
                window.removeEventListener("pagehide", pageHideHandler, true);
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
        canceled = false;
    }

    function getStatus() {
        return {
            initialized: initialized,
            canceled: canceled,
            sessionId: sessionId,
            visibilityState: document.visibilityState,
            hidden: document.hidden,
            hasFocus: safeHasFocus(),
            inGracePeriod: isInGracePeriod()
        };
    }

    return {
        init,
        enforcePolicies,
        hardCancel,
        dispose,
        getStatus
    };
})();