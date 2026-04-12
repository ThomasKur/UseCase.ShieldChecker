/**
 * ShieldChecker chart helpers — vanilla Chart.js interop.
 * Called from Blazor components via IJSRuntime.InvokeVoidAsync.
 * Chart.js itself is loaded from CDN in _Layout.cshtml.
 */
window.ShieldCheckerCharts = {
    /**
     * Create (or recreate) a Chart.js chart on the given canvas element.
     * @param {string} canvasId - id attribute of the <canvas> element
     * @param {object} config   - full Chart.js configuration object
     */
    createChart: function (canvasId, config) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        // Destroy a previously created chart on this canvas to avoid duplicates.
        const existing = Chart.getChart(canvas);
        if (existing) {
            existing.destroy();
        }

        new Chart(canvas, config);
    }
};
