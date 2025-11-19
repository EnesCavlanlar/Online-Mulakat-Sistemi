// wwwroot/js/exam-media.js

window.mediaGate = {
    /**
     * Kamera + mikrofon izinlerini ister, alınırsa videoya streami bağlar.
     * videoElementId: <video> elementinin id'si
     * statusElementId: durum gösterecek span/div id'si
     * return: true = başarı, false = hata
     */
    request: async function (videoElementId, statusElementId) {
        const video = document.getElementById(videoElementId);
        const status = document.getElementById(statusElementId);

        if (!video) {
            console.error("mediaGate: video element not found:", videoElementId);
            return false;
        }

        if (status) {
            status.textContent = "İzin isteniyor...";
        }

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            if (status) {
                status.textContent = "Tarayıcı kamerayı desteklemiyor.";
            }
            console.error("mediaGate: getUserMedia not supported");
            return false;
        }

        try {
            const stream = await navigator.mediaDevices.getUserMedia({
                video: true,
                audio: true
            });

            video.srcObject = stream;
            video.play().catch(err => console.warn("video.play error", err));

            if (status) {
                status.textContent = "Kamera/Mikrofon aktif";
            }

            return true;
        } catch (err) {
            console.error("mediaGate: getUserMedia error", err);
            if (status) {
                status.textContent = "İzin reddedildi veya hata oluştu.";
            }
            return false;
        }
    },

    /**
     * İstersen ileride çağırmak için: kamerayı kapatır.
     */
    stop: function (videoElementId, statusElementId) {
        const video = document.getElementById(videoElementId);
        const status = document.getElementById(statusElementId);

        if (video && video.srcObject) {
            const tracks = video.srcObject.getTracks();
            tracks.forEach(t => t.stop());
            video.srcObject = null;
        }

        if (status) {
            status.textContent = "Kamera kapatıldı.";
        }
    }
};
