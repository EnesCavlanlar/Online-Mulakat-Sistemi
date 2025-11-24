/* Your Global Scripts */
window.examRunner = window.examRunner || {
    closeWindow: function () {
        try { window.close(); } catch (e) { }
        setTimeout(function () {
            try { window.location.href = "/exam/finished"; } catch (e) { }
        }, 1500);
    }
};
