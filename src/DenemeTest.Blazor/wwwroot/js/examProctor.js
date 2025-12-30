// Sekme değişimi, blur, Alt+Tab, reload gibi ihlallerde .NET'e iptal çağrısı yapar.
window.examProctor = (function () {
    let dotnet = null, sessionId = null, canceled = false;

    function init(dotnetRef, sessId) {
        dotnet = dotnetRef; sessionId = sessId;
        enforcePolicies();
    }

    function enforcePolicies() {
        try {
            // 1) Sayfa reload edilmişse
            const nav = performance.getEntriesByType && performance.getEntriesByType("navigation");
            const type = nav && nav[0] ? nav[0].type :
                (performance.navigation && performance.navigation.type === 1 ? "reload" : "navigate");
            if (type === "reload") return hardCancel("Sayfa yenilendi");

            // 2) Odak/Görünürlük
            window.addEventListener("blur", () => hardCancel("Odak kaybı"));
            document.addEventListener("visibilitychange", () => {
                if (document.visibilityState !== "visible") hardCancel("Sekme değiştirildi / görünürlük kaybı");
            });

            // 3) Alt+Tab
            window.addEventListener("keydown", (e) => {
                if (e.altKey && (e.key === "Tab" || e.code === "Tab")) {
                    hardCancel("Alt+Tab algılandı");
                }
            }, { capture: true });

            // 4) Ekran paylaşımı durduruldu sinyali recorder.js tarafından da iptal ettirilecek.
        } catch (e) {
            console.warn("[examProctor] enforcePolicies error", e);
        }
    }

    async function hardCancel(reason) {
        if (canceled) return;
        canceled = true;
        try {
            if (dotnet) { await dotnet.invokeMethodAsync("CancelByPolicy", reason || "Policy"); }
        } catch (e) {
            // server fallback (gerekirse)
            try { await fetch(`/api/exam/cancel?sessionId=${encodeURIComponent(sessionId)}&reason=${encodeURIComponent(reason || "Policy")}`, { method: "POST" }); } catch { }
        }
        try { window.close(); } catch { }
    }

    function dispose() {
        // temizleme gerekirse
    }

    return { init, enforcePolicies, dispose };
})();
