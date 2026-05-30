export function renderLineChart(canvas, labels, data, datasetLabel, isPositive = true) {
    if (!window.Chart) {
        console.error("Chart.js is not loaded.");
        return null;
    }

    if (!canvas || typeof canvas.getContext !== 'function') {
        console.warn("Canvas element is null, undefined, or invalid.");
        return null;
    }

    try {
        const existingChart = window.Chart.getChart(canvas);
        if (existingChart) {
            existingChart.destroy();
        }
    } catch (e) {
        console.warn("Failed to check or destroy existing chart", e);
    }

    const ctx = canvas.getContext('2d');
    const gradient = ctx.createLinearGradient(0, 0, 0, 400);
    const color = isPositive ? '#ccff00' : '#ff453a';
    const gradientColor = isPositive ? 'rgba(204, 255, 0, 0.4)' : 'rgba(255, 69, 58, 0.4)';
    const gradientEndColor = isPositive ? 'rgba(204, 255, 0, 0.0)' : 'rgba(255, 69, 58, 0.0)';

    gradient.addColorStop(0, gradientColor);
    gradient.addColorStop(1, gradientEndColor);

    const config = {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: datasetLabel,
                data: data,
                borderColor: color,
                backgroundColor: gradient,
                borderWidth: 3,
                pointBackgroundColor: '#0d0d0d',
                pointBorderColor: color,
                pointBorderWidth: 2,
                pointRadius: 4,
                pointHoverRadius: 6,
                fill: true,
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: '#1c1c1e',
                    titleColor: color,
                    bodyColor: '#ffffff',
                    borderColor: '#3f3f46',
                    borderWidth: 1,
                    padding: 10,
                    displayColors: false,
                    callbacks: {
                        label: function (context) {
                            return context.parsed.y;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        display: false,
                        drawBorder: false
                    },
                    ticks: {
                        color: '#a1a1aa', // Zinc-400
                        font: {
                            family: "'Inter', sans-serif",
                            size: 11
                        }
                    }
                },
                y: {
                    grid: {
                        color: '#27272a', // Zinc-800
                        drawBorder: false,
                        borderDash: [5, 5]
                    },
                    ticks: {
                        color: '#a1a1aa',
                        font: {
                            family: "'Inter', sans-serif",
                            size: 11
                        },
                        padding: 10
                    },
                    beginAtZero: false
                }
            },
            interaction: {
                mode: 'nearest',
                axis: 'x',
                intersect: false
            }
        }
    };

    return new window.Chart(ctx, config);
}

export function updateChart(chartInstance, labels, data, datasetLabel, isPositive = true) {
    if (chartInstance) {
        chartInstance.data.labels = labels;
        chartInstance.data.datasets[0].data = data;
        chartInstance.data.datasets[0].label = datasetLabel;

        const color = isPositive ? '#ccff00' : '#ff453a';
        const ctx = chartInstance.ctx;
        const gradient = ctx.createLinearGradient(0, 0, 0, 400);
        const gradientColor = isPositive ? 'rgba(204, 255, 0, 0.4)' : 'rgba(255, 69, 58, 0.4)';
        const gradientEndColor = isPositive ? 'rgba(204, 255, 0, 0.0)' : 'rgba(255, 69, 58, 0.0)';

        gradient.addColorStop(0, gradientColor);
        gradient.addColorStop(1, gradientEndColor);

        chartInstance.data.datasets[0].borderColor = color;
        chartInstance.data.datasets[0].backgroundColor = gradient;
        chartInstance.data.datasets[0].pointBorderColor = color;
        if (chartInstance.options.plugins && chartInstance.options.plugins.tooltip) {
            chartInstance.options.plugins.tooltip.titleColor = color;
        }

        chartInstance.update();
    }
}

export function destroyChart(chartInstance) {
    if (chartInstance) {
        chartInstance.destroy();
    }
}

export function renderSparklineChart(canvas, labels, data, isPositive = true) {
    if (!window.Chart) {
        console.error("Chart.js is not loaded.");
        return null;
    }

    if (!canvas || typeof canvas.getContext !== 'function') {
        console.warn("Canvas element is null, undefined, or invalid.");
        return null;
    }

    try {
        const existingChart = window.Chart.getChart(canvas);
        if (existingChart) {
            existingChart.destroy();
        }
    } catch (e) {
        console.warn("Failed to check or destroy existing chart", e);
    }

    const ctx = canvas.getContext('2d');
    const chartHeight = canvas.clientHeight || canvas.height || 100;
    const gradient = ctx.createLinearGradient(0, 0, 0, chartHeight);
    const color = isPositive ? '#ccff00' : '#ff453a';
    const gradientColor = isPositive ? 'rgba(204, 255, 0, 0.6)' : 'rgba(255, 69, 58, 0.6)';
    const gradientEndColor = isPositive ? 'rgba(204, 255, 0, 0.0)' : 'rgba(255, 69, 58, 0.0)';

    gradient.addColorStop(0, gradientColor);
    gradient.addColorStop(1, gradientEndColor);

    const config = {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                borderColor: color,
                backgroundColor: gradient,
                borderWidth: 2,
                pointRadius: 0,
                pointHoverRadius: 0,
                fill: true,
                tension: 0.4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: { enabled: false }
            },
            scales: {
                x: { display: false },
                y: { display: false, beginAtZero: false }
            },
            interaction: {
                mode: 'index',
                intersect: false
            },
            layout: {
                padding: 0
            }
        }
    };

    return new window.Chart(ctx, config);
}
