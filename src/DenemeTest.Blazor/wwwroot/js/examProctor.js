// wwwroot/js/examProctor.js
// Sekme/uygulama değişimini %99+ yakalamak için:
// - blur / focusout / visibilitychange / pagehide / freeze
// - watchdog: hasFocus + visibilityState her 100ms
// - rAF gap dedektörü (tab gizlenince rAF durur)

window.examProctor = (function () {
    let dotnetRef = null;
    let sessionId = null;
    let armed = false;

    let watchdog = null;
    let rafId = null;
    let lastRafTs = 0;

    function init(ref, sessId) {
        dotnetRef = ref;
        sessionId = sessId;
        armed = true;

        // OLAYLAR
        window.addEventListener('blur', onBlur, true);
        window.addEventListener('focusout', onBlur, true);
        document.addEventListener('visibilitychange', onVisibility, true);
        window.addEventListener('pagehide', onPageHide, true);
        document.addEventListener('freeze', onFreeze, true);

        // Bazı kombinasyonlar (bonus)
        window.addEventListener('keydown', onKeyDown, true);

        // WATCHDOG (100ms): hasFocus + visibility
        stopWatchdog();
        watchdog = setInterval(() => {
            if (!armed) return;
            const hidden = document.visibilityState === 'hidden';
            const focused = document.hasFocus();
            if (hidden || !focused) {
                fireAutoCancel(hidden ? 'watchdog:visibility-hidden' : 'watchdog:no-focus');
            }
        }, 100);

        // rAF GAP DEDT.
        lastRafTs = performance.now();
        const loop = (ts) => {
            if (!armed) return; // durdu
            // 200ms'den büyük rAF boşluğu -> tab gizlendi/uygulama arka plana gitti
            if (ts - lastRafTs > 200) {
                fireAutoCancel('raf-gap');
                return;
            }
            lastRafTs = ts;
            rafId = requestAnimationFrame(loop);
        };
        rafId = requestAnimationFrame(loop);

        console.log('[proctor] armed for session', sessionId);
    }

    function dispose() {
        armed = false;

        window.removeEventListener('blur', onBlur, true);
        window.removeEventListener('focusout', onBlur, true);
        document.removeEventListener('visibilitychange', onVisibility, true);
        window.removeEventListener('pagehide', onPageHide, true);
        document.removeEventListener('freeze', onFreeze, true);
        window.removeEventListener('keydown', onKeyDown, true);

        stopWatchdog();

        if (rafId) cancelAnimationFrame(rafId);
        rafId = null;

        dotnetRef = null;
        sessionId = null;
    }

    function stopWatchdog() {
        if (watchdog) {
            clearInterval(watchdog);
            watchdog = null;
        }
    }

    // --- Handlers ---
    function onBlur() {
        if (!armed) return;
        fireAutoCancel('blur/focusout');
    }

    function onVisibility() {
        if (!armed) return;
        if (document.visibilityState === 'hidden') {
            fireAutoCancel('visibility-hidden');
        }
    }

    function onPageHide() {
        if (!armed) return;
        fireAutoCancel('pagehide');
    }

    function onFreeze() {
        if (!armed) return;
        fireAutoCancel('freeze');
    }

    function onKeyDown(e) {
        if (!armed) return;
        // Bazı tarayıcılar Alt+Tab'i yakalatmaz ama yakalarsa yine iptal
        if ((e.altKey || e.metaKey) && e.key === 'Tab') {
            e.preventDefault();
            fireAutoCancel('key:alt/meta+tab');
        }
    }

    async function fireAutoCancel(reason) {
        if (!armed || !dotnetRef) return;
        armed = false; // tek sefer
        try {
            await dotnetRef.invokeMethodAsync('FocusLostAutoCancel', reason || null);
        } catch (err) {
            console.error('[proctor] FocusLostAutoCancel invoke failed:', err);
        }
        // event/interval temizliği
        dispose();
    }

    return { init, dispose };
})();
