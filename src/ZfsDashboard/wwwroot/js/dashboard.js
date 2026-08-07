(function () {
    const dashboard = document.getElementById('dashboard');
    if (!dashboard) return;

    let failures = 0;

    function escapeHtml(value) {
        return String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element && value !== null && value !== undefined) element.textContent = value;
    }

    function formatRate(value) {
        const units = ['B/s', 'KiB/s', 'MiB/s', 'GiB/s'];
        for (let i = 0; i < units.length; i++) {
            if (value < 1024 || i === units.length - 1) return value.toFixed(1) + ' ' + units[i];
            value /= 1024;
        }
    }

    function rollingChart(canvasId, label0, color0, label1, color1) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return function () {};

        const labels = [];
        const data0 = [];
        const data1 = [];
        const chart = new Chart(canvas, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    { label: label0, data: data0, borderColor: color0, backgroundColor: color0 + '18', fill: true, tension: 0.3, pointRadius: 0, borderWidth: 1.5 },
                    { label: label1, data: data1, borderColor: color1, backgroundColor: color1 + '18', fill: true, tension: 0.3, pointRadius: 0, borderWidth: 1.5 },
                ],
            },
            options: {
                animation: false,
                responsive: true,
                aspectRatio: 4,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { boxWidth: 10, color: '#adb5bd', font: { size: 11 } } },
                    tooltip: { callbacks: { label: context => context.dataset.label + ': ' + formatRate(context.parsed.y) } },
                },
                scales: {
                    x: { ticks: { display: false }, grid: { color: 'rgba(255,255,255,0.05)' } },
                    y: { beginAtZero: true, grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#adb5bd', font: { size: 10 }, callback: formatRate } },
                },
            },
        });
        return function (value0, value1) {
            labels.push('');
            data0.push(value0);
            data1.push(value1);
            if (labels.length > 60) {
                labels.shift();
                data0.shift();
                data1.shift();
            }
            chart.update();
        };
    }

    function gaugeColor(percentage) {
        return percentage > 85 ? '#dc3545' : percentage > 70 ? '#ffc107' : '#0dcaf0';
    }

    function createGauge(canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return null;

        const size = 140;
        canvas.width = size;
        canvas.height = size;

        const initialValue = Number.parseFloat(canvas.dataset.initialValue);
        const percentage = Number.isFinite(initialValue) ? initialValue : 0;
        return new Chart(canvas, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: [percentage, 100 - percentage],
                    backgroundColor: [gaugeColor(percentage), 'rgba(255,255,255,0.08)'],
                    borderWidth: 0,
                }],
            },
            options: {
                cutout: '75%',
                responsive: false,
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
                animation: { duration: 300 },
            },
        });
    }

    function updateGauge(chart, elementId, percentage) {
        if (!chart || !Number.isFinite(percentage)) return;
        chart.data.datasets[0].data = [percentage, 100 - percentage];
        chart.data.datasets[0].backgroundColor[0] = gaugeColor(percentage);
        chart.update();
        setText(elementId, percentage.toFixed(1) + '%');
    }

    function renderNetworkRows(rates) {
        const body = document.getElementById('netDetail');
        if (!body) return;
        if (!rates.length) {
            body.innerHTML = '<tr><td colspan="3" class="text-body-secondary">No active interfaces</td></tr>';
            return;
        }

        body.innerHTML = rates.map(rate => `
            <tr class="border-bottom border-secondary">
                <td class="fw-semibold font-monospace">${escapeHtml(rate.name)}</td>
                <td class="text-end font-monospace text-info">${escapeHtml(rate.downloadRate)}</td>
                <td class="text-end font-monospace text-warning">${escapeHtml(rate.uploadRate)}</td>
            </tr>`).join('');
    }

    function renderDiskRows(rates) {
        const body = document.getElementById('diskDetail');
        if (!body) return;
        if (!rates.length) {
            body.innerHTML = '<tr><td colspan="5" class="text-body-secondary">No disk data</td></tr>';
            return;
        }

        body.innerHTML = rates.map(rate => `
            <tr class="border-bottom border-secondary">
                <td class="fw-semibold font-monospace">${escapeHtml(rate.device)}</td>
                <td class="text-end font-monospace text-info">${escapeHtml(rate.readRate)}</td>
                <td class="text-end font-monospace text-warning">${escapeHtml(rate.writeRate)}</td>
                <td class="text-end font-monospace ${escapeHtml(rate.temperatureCss)}">${rate.temperature === null ? '&ndash;' : escapeHtml(rate.temperature) + '&deg;C'}</td>
                <td class="text-end font-monospace ${escapeHtml(rate.utilizationCss)}">${rate.utilizationPercent.toFixed(1)}%</td>
            </tr>`).join('');
    }

    function renderPoolDiskRows(pool) {
        const body = document.getElementById('poolDisks-' + pool.name);
        if (!body) return;
        if (!pool.disks.length) {
            body.innerHTML = '<tr><td colspan="6" class="text-body-secondary">No disk data</td></tr>';
            return;
        }

        body.innerHTML = pool.disks.map(disk => `
            <tr class="border-bottom border-secondary">
                <td class="fw-semibold font-monospace">${escapeHtml(disk.device)}</td>
                <td><span class="badge bg-secondary">${escapeHtml(disk.vdevType || '\u2013')}</span></td>
                <td class="text-end font-monospace">${escapeHtml(disk.queueDepth)}</td>
                <td class="text-end font-monospace">${escapeHtml(disk.readLatency)}</td>
                <td class="text-end font-monospace">${escapeHtml(disk.writeLatency)}</td>
                <td class="text-end font-monospace ${escapeHtml(disk.utilizationCss)}">${disk.utilizationPercent.toFixed(1)}%</td>
            </tr>`).join('');
    }

    function updatePoolSummary(pool) {
        const summary = pool.summary;
        if (!summary) return;

        setText('poolSize-' + pool.name, summary.size);
        setText('poolAllocated-' + pool.name, summary.allocated);
        setText('poolFree-' + pool.name, summary.free);
        setText('poolCapacity-' + pool.name, summary.usagePercent.toFixed(0) + '%');

        const health = document.getElementById('poolHealth-' + pool.name);
        if (health) {
            health.className = 'badge ' + summary.healthCss;
            health.textContent = summary.health;
        }

        const encrypted = document.getElementById('poolEncrypted-' + pool.name);
        if (encrypted) {
            encrypted.classList.toggle('d-none', !summary.encrypted);
            updateTooltip(encrypted, summary.encrypted ? 'Encrypted (' + summary.encryptionAlgorithm + ')' : '');
        }

        const errors = document.getElementById('poolErrors-' + pool.name);
        if (errors) {
            errors.classList.toggle('d-none', !summary.hasErrors);
            updateTooltip(errors, summary.hasErrors ? summary.errorTooltip : '');
        }

        const progress = document.querySelector('#poolCapacityBar-' + CSS.escape(pool.name) + ' .progress-bar');
        if (progress) {
            progress.style.width = Math.max(0, Math.min(100, summary.usagePercent)).toFixed(1) + '%';
            progress.className = 'progress-bar ' + (summary.usagePercent > 85 ? 'bg-danger' : summary.usagePercent > 70 ? 'bg-warning' : 'bg-success');
        }
    }

    function updateTooltip(element, title) {
        if ((element.getAttribute('data-bs-title') ?? '') === title) return;

        bootstrap.Tooltip.getInstance(element)?.dispose();
        element.removeAttribute('data-bs-title');
        element.removeAttribute('title');
        if (!title) return;

        element.setAttribute('data-bs-title', title);
        new bootstrap.Tooltip(element);
    }

    function renderScrub(pool) {
        const element = document.getElementById('poolScrub-' + pool.name);
        if (!element) return;
        const scrub = pool.scrub;
        const loading = element.querySelector('[data-scrub-loading]');
        const running = element.querySelector('[data-scrub-running]');
        const status = element.querySelector('[data-scrub-status]');
        if (!loading || !running || !status) return;

        loading.classList.add('d-none');
        running.classList.toggle('d-none', !scrub.isRunning);
        status.classList.toggle('d-none', scrub.isRunning);

        const target = scrub.isRunning ? running : status;
        target.querySelector('[data-scrub-headline]').textContent = scrub.headline;
        target.querySelector('[data-scrub-details]').textContent = scrub.details.join(' \u00b7 ');

        if (scrub.isRunning) {
            const progress = Math.max(0, Math.min(100, scrub.progressPercent));
            running.querySelector('[data-scrub-progress]').style.width = progress.toFixed(1) + '%';
        } else {
            status.querySelector('[data-scrub-status-label]').className = scrub.statusCss;
            status.querySelector('[data-scrub-icon]').className = 'bi ' + scrub.iconCss + ' me-1';
        }
    }

    function updateMetricRows(rows) {
        rows.forEach(row => {
            if (!row.elementId) return;
            const element = document.getElementById(row.elementId);
            if (!element) return;
            element.textContent = row.value;
            element.closest('[data-metric-row]')?.classList.toggle('d-none', !row.isVisible);
        });
    }

    function updateMemory(memory) {
        updateGauge(memoryChart, 'memPct', memory.usagePercent);
        updateMetricRows(memory.details);
    }

    function updateArc(arc) {
        const card = document.getElementById('arcCard');
        if (!card || !arcChart) return;
        card.classList.toggle('d-none', !arc.isVisible);
        if (!arc.isVisible) return;

        arcChart.resize();
        updateGauge(arcChart, 'arcPct', arc.usagePercent);
        updateMetricRows(arc.details);

        const hitRate = document.getElementById('arcHitRate');
        if (hitRate) {
            hitRate.className = 'col-7 ' + arc.hitRateCss + ' fw-semibold';
            hitRate.textContent = arc.hitRate.toFixed(1) + '%';
        }

        const l2HitRate = document.getElementById('l2HitRate');
        if (l2HitRate && arc.l2HitRate !== null) {
            l2HitRate.className = 'col-7 ' + arc.l2HitRateCss + ' fw-semibold';
            l2HitRate.textContent = arc.l2HitRate.toFixed(1) + '% (' + arc.l2Size + ')';
        }
    }

    const pushNetwork = rollingChart('netCanvas', 'Download', '#0dcaf0', 'Upload', '#ffc107');
    const pushDisk = rollingChart('diskCanvas', 'Read', '#0dcaf0', 'Write', '#ffc107');
    const cpuChart = createGauge('cpuCanvas');
    const memoryChart = createGauge('memCanvas');
    const arcChart = createGauge('arcCanvas');

    async function fetchData() {
        const controller = new AbortController();
        let timedOut = false;
        const timeoutId = setTimeout(() => {
            timedOut = true;
            controller.abort();
        }, 10000);

        try {
            const response = await fetch(dashboard.dataset.liveUrl, {
                cache: 'no-store',
                signal: controller.signal,
            });
            if (!response.ok) throw new Error('HTTP ' + response.status);
            const data = await response.json();
            failures = 0;

            setText('sysUptime', data.uptime);
            updateGauge(cpuChart, 'cpuPct', data.cpuUsagePercent);
            updateMemory(data.memory);
            updateArc(data.arc);

            const networkDownload = data.networkRates.reduce((sum, rate) => sum + rate.rxBytesPerSecond, 0);
            const networkUpload = data.networkRates.reduce((sum, rate) => sum + rate.txBytesPerSecond, 0);
            pushNetwork(networkDownload, networkUpload);
            renderNetworkRows(data.networkRates);

            const diskRead = data.diskIoRates.reduce((sum, disk) => sum + disk.readBytesPerSecond, 0);
            const diskWrite = data.diskIoRates.reduce((sum, disk) => sum + disk.writeBytesPerSecond, 0);
            pushDisk(diskRead, diskWrite);
            renderDiskRows(data.diskIoRates);

            data.pools.forEach(pool => {
                updatePoolSummary(pool);
                renderPoolDiskRows(pool);
                renderScrub(pool);
            });
            setText('poolCount', data.pools.length + ' pools');
        } catch (error) {
            failures++;
            const message = timedOut ? 'Request timed out' : error.message;
            console.error('live update failed:', message);
        } finally {
            clearTimeout(timeoutId);
            const delay = failures === 0 ? 1000 : Math.min(1000 * (2 ** (failures - 1)), 30000);
            setTimeout(fetchData, delay);
        }
    }

    fetchData();
})();
