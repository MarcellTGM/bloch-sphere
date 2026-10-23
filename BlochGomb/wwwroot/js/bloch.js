/* Bloch-gömb nézet – Plotly.js vezérlő
 * Animált állapotvektor, nyomvonal, pillanatképek, téma, PNG export.
 */
(function () {
    'use strict';

    var view = { div: null, last: null, theme: 'dark', raf: null, snapCount: 0 };

    // Gömb felületének rácspontjai (egyszer számoljuk ki).
    var SURFACE = (function () {
        var nu = 32, nv = 64, xs = [], ys = [], zs = [];
        for (var i = 0; i <= nu; i++) {
            xs[i] = []; ys[i] = []; zs[i] = [];
            var u = (i * Math.PI) / nu;
            for (var j = 0; j <= nv; j++) {
                var v = (j * 2 * Math.PI) / nv;
                xs[i][j] = Math.sin(u) * Math.cos(v);
                ys[i][j] = Math.sin(u) * Math.sin(v);
                zs[i][j] = Math.cos(u);
            }
        }
        return { x: xs, y: ys, z: zs };
    })();

    // Segédvonalak: a három tengely és a három főkör.
    var GUIDE = (function () {
        var x = [], y = [], z = [];
        function segment(ax, ay, az, bx, by, bz) {
            x.push(ax, bx, null); y.push(ay, by, null); z.push(az, bz, null);
        }
        segment(-1, 0, 0, 1, 0, 0);
        segment(0, -1, 0, 0, 1, 0);
        segment(0, 0, -1, 0, 0, 1);
        function circle(fn) {
            for (var i = 0; i <= 72; i++) {
                var p = fn((i / 72) * 2 * Math.PI);
                x.push(p.x); y.push(p.y); z.push(p.z);
            }
            x.push(null); y.push(null); z.push(null);
        }
        circle(function (t) { return { x: Math.cos(t), y: Math.sin(t), z: 0 }; });
        circle(function (t) { return { x: Math.cos(t), y: 0, z: Math.sin(t) }; });
        circle(function (t) { return { x: 0, y: Math.cos(t), z: Math.sin(t) }; });
        return { x: x, y: y, z: z };
    })();

    function palette(theme) {
        if (theme === 'light') {
            return {
                surface: [[0, 'rgba(31,94,255,0.11)'], [1, 'rgba(31,94,255,0.05)']],
                grid: '#e4e8ee', tick: '#78838f', title: '#4e5967',
                vector: '#1f5eff', point: '#1f5eff', ring: '#ffffff',
                trail: 'rgba(31,94,255,0.20)',
                guide: 'rgba(120,131,143,0.45)',
                hover: '#ffffff'
            };
        }
        return {
            surface: [[0, 'rgba(76,141,255,0.18)'], [1, 'rgba(76,141,255,0.08)']],
            grid: '#222a33', tick: '#7a8695', title: '#a4aebb',
            vector: '#4c8dff', point: '#4c8dff', ring: '#14181d',
            trail: 'rgba(76,141,255,0.22)',
            guide: 'rgba(122,134,149,0.40)',
            hover: '#14181d'
        };
    }

    function axis(name, c) {
        return {
            title: { text: name, font: { color: c.title, size: 11 } },
            range: [-1.05, 1.05],
            showbackground: false,
            gridcolor: c.grid,
            zerolinecolor: c.grid,
            tickcolor: c.grid,
            tickfont: { color: c.tick, size: 9 },
            showspikes: false
        };
    }

    function buildLayout(c) {
        return {
            paper_bgcolor: 'rgba(0,0,0,0)',
            plot_bgcolor: 'rgba(0,0,0,0)',
            margin: { l: 0, r: 0, t: 0, b: 0 },
            showlegend: false,
            hovermode: 'closest',
            hoverlabel: { bgcolor: c.hover, bordercolor: c.grid, font: { color: c.title, size: 11 } },
            scene: {
                bgcolor: 'rgba(0,0,0,0)',
                xaxis: axis('X', c),
                yaxis: axis('Y', c),
                zaxis: axis('Z', c),
                aspectmode: 'cube',
                camera: { eye: { x: 1.18, y: 1.02, z: 0.82 }, center: { x: 0, y: 0, z: 0 } }
            }
        };
    }

    function buildTraces(payload, c, vec, trail) {
        var data = [];

        // 0 – a gömb felülete
        data.push({
            type: 'surface', x: SURFACE.x, y: SURFACE.y, z: SURFACE.z,
            opacity: 0.9, showscale: false, colorscale: c.surface,
            lighting: { ambient: 0.9, diffuse: 0.25, specular: 0.05, roughness: 1.0 },
            hoverinfo: 'skip', name: 'Bloch-gömb'
        });

        // 1 – bejárt út
        data.push({
            type: 'scatter3d', mode: 'lines+markers',
            x: trail.map(function (q) { return q.x; }),
            y: trail.map(function (q) { return q.y; }),
            z: trail.map(function (q) { return q.z; }),
            line: { color: c.trail, width: 2 },
            marker: { size: 2, color: c.trail },
            hoverinfo: 'skip', name: 'Nyomvonal'
        });

        // 2 – állapotvektor
        data.push({
            type: 'scatter3d', mode: 'lines',
            x: [0, vec.x], y: [0, vec.y], z: [0, vec.z],
            line: { color: c.vector, width: 5 },
            name: 'Állapotvektor',
            hovertemplate: 'x: %{x:.3f}<br>y: %{y:.3f}<br>z: %{z:.3f}<extra>Állapotvektor</extra>'
        });

        // 3 – végpont
        data.push({
            type: 'scatter3d', mode: 'markers',
            x: [vec.x], y: [vec.y], z: [vec.z],
            marker: { size: 6, color: c.point, line: { width: 1.5, color: c.ring } },
            name: 'Aktuális állapot',
            hovertemplate: '|ψ⟩<br>x: %{x:.3f}<br>y: %{y:.3f}<br>z: %{z:.3f}<extra></extra>'
        });

        // 4 – segédvonalak (tengelyek + főkörök)
        data.push({
            type: 'scatter3d', mode: 'lines',
            x: GUIDE.x, y: GUIDE.y, z: GUIDE.z,
            line: { color: c.guide, width: 1 },
            hoverinfo: 'skip', name: 'Segédvonalak'
        });

        // 5.. – pillanatképek
        (payload.snapshots || []).forEach(function (s) {
            data.push({
                type: 'scatter3d', mode: 'markers',
                x: [s.x], y: [s.y], z: [s.z],
                marker: { size: 6, color: s.color, symbol: 'diamond', line: { width: 1, color: c.ring } },
                name: s.label,
                hovertemplate: s.label + '<br>x: %{x:.3f}<br>y: %{y:.3f}<br>z: %{z:.3f}<extra></extra>'
            });
        });

        return data;
    }

    function animateVector(from, to, c, fullTrail) {
        if (view.raf) { cancelAnimationFrame(view.raf); view.raf = null; }
        var start = null, duration = 420;

        function step(now) {
            if (start === null) start = now;
            var k = Math.min(1, (now - start) / duration);
            var e = 1 - Math.pow(1 - k, 3);   // ease-out
            var vx = from.x + (to.x - from.x) * e;
            var vy = from.y + (to.y - from.y) * e;
            var vz = from.z + (to.z - from.z) * e;

            Plotly.restyle(view.div,
                { x: [[0, vx], [vx]], y: [[0, vy], [vy]], z: [[0, vz], [vz]] },
                [2, 3]);

            if (k < 1) {
                view.raf = requestAnimationFrame(step);
            } else {
                view.raf = null;
                // a nyomvonal utolsó pontját a mozgás végén rajzoljuk be
                Plotly.restyle(view.div, {
                    x: [fullTrail.map(function (q) { return q.x; })],
                    y: [fullTrail.map(function (q) { return q.y; })],
                    z: [fullTrail.map(function (q) { return q.z; })]
                }, [1]);
            }
        }
        view.raf = requestAnimationFrame(step);
    }

    var api = {
        init: function (divId) {
            view.div = document.getElementById(divId);
            if (!view.div || typeof Plotly === 'undefined') { return; }
            api.setTheme(document.documentElement.getAttribute('data-theme') || 'dark');

            if (!window.__blochKeysBound) {
                window.__blochKeysBound = true;
                document.addEventListener('keydown', function (e) {
                    if (e.ctrlKey || e.altKey || e.metaKey) return;
                    var tag = (e.target && e.target.tagName) ? e.target.tagName.toLowerCase() : '';
                    if (tag === 'input' || tag === 'select' || tag === 'textarea') return;
                    var key = e.key === 'Enter' ? 'ENTER'
                        : e.key === 'Backspace' ? 'BACKSPACE'
                        : e.key === 'Escape' ? 'ESCAPE'
                        : e.key.toUpperCase();
                    var btn = document.querySelector('[data-key="' + key + '"]');
                    if (btn) { e.preventDefault(); btn.click(); }
                });
            }
        },

        update: function (payload) {
            if (!view.div) { api.init('sphere'); }
            if (!view.div || typeof Plotly === 'undefined') { return; }

            view.theme = payload.theme || view.theme;
            var c = palette(view.theme);
            var target = { x: Number(payload.x) || 0, y: Number(payload.y) || 0, z: Number(payload.z) || 0 };
            var trail = (payload.trail || []).map(function (q) {
                return { x: Number(q.x) || 0, y: Number(q.y) || 0, z: Number(q.z) || 0 };
            });
            var from = view.last || target;
            var distance = Math.abs(from.x - target.x) + Math.abs(from.y - target.y) + Math.abs(from.z - target.z);
            var animate = payload.animate === true && distance > 1e-6;

            // animáció nélkül azonnal a célállapotot rajzoljuk
            var renderVec = animate ? from : target;

            // animáció közben a nyomvonal utolsó (cél)pontját még nem rajzoljuk ki
            var trailForRender = (animate && trail.length > 1) ? trail.slice(0, trail.length - 1) : trail;

            Plotly.react(view.div, buildTraces(payload, c, renderVec, trailForRender), buildLayout(c), {
                responsive: true,
                displayModeBar: false,
                staticPlot: false
            });

            if (animate) {
                animateVector(from, target, c, trail);
            }
            view.last = target;
            view.snapCount = (payload.snapshots || []).length;
        },

        setTheme: function (theme) {
            view.theme = theme;
            document.documentElement.setAttribute('data-theme', theme);
            try { localStorage.setItem('bloch-theme', theme); } catch (e) { /* privát mód */ }
            if (view.div && view.last) {
                Plotly.relayout(view.div, buildLayout(palette(theme)));
            }
        },

        getTheme: function () {
            return document.documentElement.getAttribute('data-theme') || 'dark';
        },

        resetView: function () {
            if (!view.div) return;
            Plotly.relayout(view.div, { 'scene.camera': { eye: { x: 1.18, y: 1.02, z: 0.82 }, center: { x: 0, y: 0, z: 0 } } });
        },

        exportPng: function (fileName) {
            if (!view.div) return;
            Plotly.toImage(view.div, { format: 'png', width: 1400, height: 1050 })
                .then(function (url) {
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = (fileName || 'bloch-gomb') + '.png';
                    document.body.appendChild(a);
                    a.click();
                    a.remove();
                });
        }
    };

    window.blochgomb = api;
})();



