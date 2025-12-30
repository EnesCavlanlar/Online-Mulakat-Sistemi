// Küçük yardýmcýlar

// Sýnav penceresini kapatmaya çalýþ (popuplarda çalýþýr, yoksa redirect)
window.examRunner = {
    closeWindow: function () {
        try {
            window.close();
            // Tarayýcý kapatmadýysa ana sayfaya savur
            setTimeout(() => {
                if (!window.closed) location.href = "/";
            }, 150);
        } catch {
            location.href = "/";
        }
    }
};

// Runner içinden JS iptal tetiklemek isterse (recorder.js ile köprü)
window.dotnetRunnerCancel = async function (reason) {
    try {
        // Blazor'dan .NET methodunu çaðýracaðýz; burada sadece event zincirini tetikleyip býrakýyoruz.
        // examProctor zaten CancelByPolicy çaðýracak.
        console.warn("Cancel requested from JS:", reason);
    } catch { }
};
