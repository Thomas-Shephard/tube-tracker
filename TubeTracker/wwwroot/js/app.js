function isLoggedIn() {
    return localStorage.getItem('token') !== null;
}

function getDecodedToken() {
    const token = localStorage.getItem('token');
    if (!token) return null;
    try {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(window.atob(base64).split('').map(function(c) {
            return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
        }).join(''));
        return JSON.parse(jsonPayload);
    } catch (e) {
        return null;
    }
}

function isVerified() {
    const payload = getDecodedToken();
    return payload ? payload.is_verified === 'true' : false;
}

function getAuthHeader() {
    const token = localStorage.getItem('token');
    return token ? { 'Authorization': `Bearer ${token}` } : {};
}

function updateNavbar() {
    const navItems = document.getElementById('nav-items');
    if (!navItems) return;

    if (isLoggedIn()) {
        navItems.innerHTML = `
            <li class="nav-item">
                <a class="nav-link" href="/">Dashboard</a>
            </li>
            <li class="nav-item">
                <a class="nav-link" href="/tracking.html">Manage Tracking</a>
            </li>
            <li class="nav-item">
                <a class="nav-link" href="/account.html">Account</a>
            </li>
            <li class="nav-item">
                <a class="nav-link btn btn-outline-light ms-lg-3 px-4" href="#" onclick="logout()">Logout</a>
            </li>
        `;
    } else {
        navItems.innerHTML = `
            <li class="nav-item">
                <a class="nav-link" href="/#status">Live Status</a>
            </li>
            <li class="nav-item">
                <a class="nav-link btn btn-outline-light ms-lg-3 px-4" href="/login.html">Login</a>
            </li>
            <li class="nav-item">
                <a class="nav-link btn btn-primary ms-lg-2 px-4" href="/register.html">Register</a>
            </li>
        `;
    }
}

function joinList(list) {
    if (list.length <= 1) return list[0] || "";
    if (list.length === 2) return `${list[0]} & ${list[1]}`;
    return list.slice(0, -1).join(", ") + ", & " + list[list.length - 1];
}

function formatSeverity(activeStatuses) {
    if (!activeStatuses || activeStatuses.length === 0) {
        return { display: "Good Service", full: [], hasDetails: false };
    }

    // Sort by urgency descending (higher urgency first), then severityLevel ascending (lower number is more severe)
    const sorted = [...activeStatuses].sort((a, b) =>
        (b.severity.urgency - a.severity.urgency) || (a.severity.severityLevel - b.severity.severityLevel)
    );

    const descriptions = [...new Set(sorted.map(s => s.severity.description))];
    
    // Create detailed status objects for modal
    const details = sorted.map(s => ({
        description: s.severity.description,
        reason: s.statusDescription || s.reason || "" // Handle both API property names
    }));

    const fullListText = joinList(descriptions);

    if (descriptions.length === 2) {
        return { display: fullListText, full: details, hasDetails: details.some(d => d.reason) };
    } else if (descriptions.length > 2) {
        return { display: `${descriptions[0]} & More`, full: details, hasDetails: true };
    }
    
    return { display: descriptions[0], full: details, hasDetails: details.some(d => d.reason) };
}

function validatePassword(password) {
    const minLength = password.length >= 8;
    const hasUpper = /[A-Z]/.test(password);
    const hasLower = /[a-z]/.test(password);
    const hasDigit = /[0-9]/.test(password);
    
    const count = [minLength, hasUpper, hasLower, hasDigit].filter(Boolean).length;
    
    return {
        isValid: minLength && hasUpper && hasLower && hasDigit,
        score: count, // 0 to 4
        requirements: { minLength, hasUpper, hasLower, hasDigit }
    };
}

async function logout() {
    try {
        await fetch('/api/auth/logout', {
            method: 'POST',
            headers: getAuthHeader()
        });
    } catch (e) {
        console.error('Logout error:', e);
    }
    localStorage.removeItem('token');
    window.location.href = '/';
}

let lastUpdateLineTime = null;
let lastUpdateTrackedTime = null;

function updateTimeAgo(elementId, timestamp) {
    const el = document.getElementById(elementId);
    if (!el || !timestamp) return;

    // Don't overwrite error messages
    if (el.querySelector('.text-danger')) return;

    const seconds = Math.floor((new Date() - timestamp) / 1000);
    
    let text = "";
    if (seconds < 10) text = "Just now";
    else if (seconds < 60) text = `${seconds} seconds ago`;
    else if (seconds < 120) text = "1 minute ago";
    else text = `${Math.floor(seconds / 60)} minutes ago`;

    // Special state for nearing refresh (assuming 60s interval)
    if (seconds >= 55) {
        el.innerHTML = `<span class="text-primary"><span class="spinner-border spinner-border-sm me-1"></span>Refreshing...</span>`;
    } else {
        el.innerText = `Last updated: ${text}`;
    }
}

function showSkeleton(containerId, count = 6) {
    const container = document.getElementById(containerId);
    if (!container) return;
    let html = '';
    for (let i = 0; i < count; i++) {
        html += `
            <div class="col-md-6 col-lg-4">
                <div class="skeleton-card shadow-sm mb-4"></div>
            </div>
        `;
    }
    container.innerHTML = html;
}

async function loadTubeStatus() {
    const resultElement = document.getElementById('api-result');
    const listContainer = document.getElementById('tube-list');
    if (!listContainer) return;

    if (!lastUpdateLineTime) {
        resultElement.innerText = "Fetching latest data...";
        showSkeleton('tube-list', 11);
    }
    
    try {
        const response = await fetch('/api/status/lines?_=' + new Date().getTime());
        if (response.ok) {
            let lines = await response.json();
            lastUpdateLineTime = new Date();
            updateTimeAgo('api-result', lastUpdateLineTime);
            listContainer.innerHTML = '';

            // Handle both "statuses" and "Statuses" from API
            lines = lines.map(line => {
                const activeStatuses = line.statuses || line.Statuses || [];
                return {
                    ...line,
                    activeStatuses: activeStatuses,
                    minSeverityId: activeStatuses.length > 0 
                        ? Math.min(...activeStatuses.map(s => s.severity.severityLevel)) 
                        : 10
                };
            }).sort((a, b) => (a.minSeverityId - b.minSeverityId) || a.name.localeCompare(b.name));

            lines.forEach((line, index) => {
                const activeStatuses = line.activeStatuses;
                const { display: severityDescription, full: details, hasDetails } = formatSeverity(activeStatuses);
                const reasons = [...new Set(activeStatuses.map(s => s.reason).filter(r => r))];
                
                const maxUrgency = activeStatuses.length ? Math.max(...activeStatuses.map(s => s.severity.urgency)) : 0;
                
                let badgeClass = "bg-success";
                let statusClass = "status-good";
                
                if (maxUrgency >= 2) {
                    badgeClass = "bg-danger";
                    statusClass = "status-severe";
                } else if (maxUrgency === 1) {
                    badgeClass = "bg-warning text-dark";
                    statusClass = "status-minor";
                }

                const cardHtml = createCardHtml(line.name, severityDescription, badgeClass, statusClass, reasons, false, details, hasDetails);
                const tempDiv = document.createElement('div');
                tempDiv.innerHTML = cardHtml;
                const cardEl = tempDiv.firstElementChild;
                cardEl.classList.add('fade-in');
                cardEl.style.animationDelay = `${index * 0.05}s`;
                listContainer.appendChild(cardEl);
            });
        } else {
            resultElement.innerHTML = `<span class="text-danger"><i class="bi bi-exclamation-circle-fill me-1"></i>Failed to update.</span>`;
        }
    } catch (e) {
        console.error(e);
        resultElement.innerHTML = `<span class="text-danger"><i class="bi bi-exclamation-circle-fill me-1"></i>Failed to update.</span>`;
    }
}

async function loadTrackedStatus() {
    if (!isLoggedIn()) return;
    
    const trackedStatusSection = document.getElementById('tracked-status');
    const unverifiedMsg = document.getElementById('tracked-unverified-msg');
    const verifiedContent = document.getElementById('tracked-verified-content');
    const lineList = document.getElementById('tracked-line-list');
    const stationList = document.getElementById('tracked-station-list');
    const resultElement = document.getElementById('tracked-api-result');
    if (!lineList) return;

    if (!isVerified()) {
        if (trackedStatusSection) trackedStatusSection.classList.remove('d-none');
        if (unverifiedMsg) unverifiedMsg.classList.remove('d-none');
        if (verifiedContent) verifiedContent.classList.add('d-none');
        if (resultElement) resultElement.innerText = "Verification required.";
        return;
    }

    if (unverifiedMsg) unverifiedMsg.classList.add('d-none');
    if (verifiedContent) verifiedContent.classList.remove('d-none');

    if (!lastUpdateTrackedTime) {
        showSkeleton('tracked-line-list', 3);
    }

    try {
        const response = await fetch('/api/status/tracked?_=' + new Date().getTime(), { headers: getAuthHeader() });
        if (response.ok) {
            const data = await response.json();
            lastUpdateTrackedTime = new Date();
            updateTimeAgo('tracked-api-result', lastUpdateTrackedTime);
            
            const emptyLineHtml = `
                <div class="col-12">
                    <div class="p-4 bg-light rounded-4 border border-dashed text-start">
                        <p class="text-muted mb-3">You are not tracking any lines yet. Start tracking to receive personalized updates and notifications.</p>
                        <a href="/tracking.html#lines" class="btn btn-primary btn-sm px-4">Track your first line</a>
                    </div>
                </div>
            `;
            const emptyStationHtml = `
                <div class="col-12">
                    <div class="p-4 bg-light rounded-4 border border-dashed text-start">
                        <p class="text-muted mb-3">You are not tracking any stations yet. Monitor specific stations for disruptions and lift closures.</p>
                        <a href="/tracking.html#stations" class="btn btn-primary btn-sm px-4">Find stations to track</a>
                    </div>
                </div>
            `;

            lineList.innerHTML = data.lines.length ? '' : emptyLineHtml;
            stationList.innerHTML = data.stations.length ? '' : emptyStationHtml;

            const sortedLines = data.lines.map(line => {
                const activeStatuses = line.statuses || line.Statuses || [];
                const maxUrgency = activeStatuses.length ? Math.max(...activeStatuses.map(s => s.severity.urgency)) : 0;
                // MinUrgency defaults to 2 (Severe) if not set, or we can use the value from the object
                const isFlagged = maxUrgency >= (line.minUrgency ?? 2) && maxUrgency > 0;
                return { ...line, activeStatuses, isFlagged, maxUrgency };
            }).sort((a, b) => {
                if (a.isFlagged && !b.isFlagged) return -1;
                if (!a.isFlagged && b.isFlagged) return 1;
                return (b.maxUrgency - a.maxUrgency) || a.name.localeCompare(b.name);
            });

            sortedLines.forEach((line, index) => {
                const activeStatuses = line.activeStatuses;
                const { display: severityDescription, full: details, hasDetails } = formatSeverity(activeStatuses);
                const reasons = [...new Set(activeStatuses.map(s => s.reason).filter(r => r))];
                const maxUrgency = line.maxUrgency || 0;
                
                let badgeClass = "bg-success";
                let statusClass = "status-good";
                
                if (maxUrgency >= 2) {
                    badgeClass = "bg-danger";
                    statusClass = "status-severe";
                } else if (maxUrgency === 1) {
                    badgeClass = "bg-warning text-dark";
                    statusClass = "status-minor";
                }
                
                const cardHtml = createCardHtml(line.name, severityDescription, badgeClass, statusClass, reasons, line.isFlagged, details, hasDetails);
                const tempDiv = document.createElement('div');
                tempDiv.innerHTML = cardHtml;
                const cardEl = tempDiv.firstElementChild;
                cardEl.classList.add('fade-in');
                cardEl.style.animationDelay = `${index * 0.05}s`;
                lineList.appendChild(cardEl);
            });

            const sortedStations = data.stations.map(station => {
                const activeStatuses = station.statuses || station.Statuses || [];
                const isFlagged = activeStatuses.length > 0 && activeStatuses.some(s => s.statusDescription !== 'No Issues');
                return { ...station, activeStatuses, isFlagged };
            }).sort((a, b) => {
                if (a.isFlagged && !b.isFlagged) return -1;
                if (!a.isFlagged && b.isFlagged) return 1;
                return a.commonName.localeCompare(b.commonName);
            });

            sortedStations.forEach((station, index) => {
                const activeStatuses = station.activeStatuses;
                const hasIssues = station.isFlagged;
                
                const badgeText = hasIssues ? "Disruption" : "No disruptions";
                const reasons = hasIssues ? activeStatuses.map(s => s.statusDescription) : [];
                
                let badgeClass = hasIssues ? "bg-warning text-dark" : "bg-success";
                let statusClass = hasIssues ? "status-minor" : "status-good";

                const cardHtml = createCardHtml(station.commonName, badgeText, badgeClass, statusClass, reasons, hasIssues);
                const tempDiv = document.createElement('div');
                tempDiv.innerHTML = cardHtml;
                const cardEl = tempDiv.firstElementChild;
                cardEl.classList.add('fade-in');
                cardEl.style.animationDelay = `${(sortedLines.length + index) * 0.05}s`;
                stationList.appendChild(cardEl);
            });
        } else if (response.status === 401) {
            logout();
        } else {
            resultElement.innerHTML = `<span class="text-danger"><i class="bi bi-exclamation-circle-fill me-1"></i>Failed to update.</span>`;
        }
    } catch (e) {
        console.error(e);
        if (resultElement) resultElement.innerHTML = `<span class="text-danger"><i class="bi bi-exclamation-circle-fill me-1"></i>Failed to update.</span>`;
    }
}

function showStatusDetail(name, detailsJson) {
    const details = JSON.parse(decodeURIComponent(detailsJson));
    const modalLabel = document.getElementById('statusModalLabel');
    const modalBody = document.getElementById('statusModalBody');
    if (modalLabel && modalBody) {
        modalLabel.innerText = `${name} - Service Details`;
        
        // Group by reason to handle merged TfL descriptions
        const grouped = details.reduce((acc, d) => {
            const key = d.reason || "no-reason";
            if (!acc[key]) acc[key] = { reason: d.reason, statuses: [] };
            acc[key].statuses.push(d.description);
            return acc;
        }, {});

        let html = '';
        Object.values(grouped).forEach((group, i) => {
            const badges = group.statuses.map(s => {
                const colorClass = s.includes('Good') ? 'bg-success' : (s.includes('Minor') ? 'bg-warning text-dark' : 'bg-danger');
                return `<span class="badge ${colorClass} me-2">${s}</span>`;
            }).join('');

            html += `
                <div class="${i > 0 ? 'mt-3 pt-3 border-top' : ''}">
                    <div class="d-flex flex-wrap align-items-center mb-2 gap-1">
                        ${badges}
                    </div>
                    ${group.reason ? `<p class="small text-muted mb-0">${group.reason}</p>` : '<p class="small text-muted mb-0 italic">No further details provided by TfL.</p>'}
                </div>
            `;
        });
        
        modalBody.innerHTML = html;
        const modal = new bootstrap.Modal(document.getElementById('statusModal'));
        modal.show();
    }
}

function createCardHtml(name, severity, badgeClass, statusClass, reasons, isFlagged = false, details = null, hasDetails = false) {
    const bell = isFlagged ? '<i class="bi bi-bell-fill me-2"></i>' : '';
    
    let infoIcon = '';
    let cardAttr = '';
    if (details && (details.length > 2 || hasDetails)) {
        const detailsJson = encodeURIComponent(JSON.stringify(details));
        const safeDetailsJson = detailsJson.replace(/'/g, "\\'");
        const safeName = name.replace(/'/g, "\\'");
        
        // Only show (i) icon if there are 3+ statuses (the "& More" case)
        if (details.length > 2) {
            infoIcon = ` <i class="bi bi-info-circle-fill ms-1"></i>`;
        }
        
        cardAttr = `onclick="showStatusDetail('${safeName}', '${safeDetailsJson}')" style="cursor: pointer;"`;
    }

    return `
        <div class="col-md-6 col-lg-4">
            <div class="card h-100 shadow-sm line-card ${statusClass} ${isFlagged ? 'flagged' : ''}" ${cardAttr}>
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <h5 class="card-title fw-bold mb-0">${bell}${name}</h5>
                        <span class="badge ${badgeClass}">${severity}${infoIcon}</span>
                    </div>
                    ${reasons.map(r => `<p class="card-text small text-muted mt-2 mb-0">${r}</p>`).join('')}
                </div>
            </div>
        </div>
    `;
}

// Global scope for onclick
window.logout = logout;
window.isLoggedIn = isLoggedIn;
window.isVerified = isVerified;
window.getAuthHeader = getAuthHeader;
window.updateNavbar = updateNavbar;
window.showStatusDetail = showStatusDetail;

// Tracking Page Logic
let allLines = [];
let trackedLines = [];
let allStations = [];
let trackedStations = [];

const urgencyLevels = [
    { val: 0, label: 'Good' },
    { val: 1, label: 'Minor' },
    { val: 2, label: 'Severe' },
    { val: 3, label: 'Critical' }
];

async function initTracking() {
    try {
        const [linesRes, trackedLinesRes, stationsRes, trackedStationsRes] = await Promise.all([
            fetch('/api/lines'),
            fetch('/api/tracking/lines', { headers: getAuthHeader() }),
            fetch('/api/stations'),
            fetch('/api/tracking/stations', { headers: getAuthHeader() })
        ]);

        allLines = await linesRes.json();
        trackedLines = await trackedLinesRes.json();
        allStations = await stationsRes.json();
        trackedStations = await trackedStationsRes.json();

        renderLines();
        renderStations();
    } catch (err) { console.error("Tracking init error:", err); }
}

function showSavedFeedback(type, id) {
    const el = document.getElementById(`saved-${type}-${id}`);
    if (el) {
        el.classList.remove('opacity-0');
        el.classList.add('opacity-100');
        setTimeout(() => {
            el.classList.remove('opacity-100');
            el.classList.add('opacity-0');
        }, 1500);
    }
}

function renderLines() {
    const container = document.getElementById('lines-list');
    if (!container) return;
    container.innerHTML = '';
    
    allLines.sort((a,b) => a.name.localeCompare(b.name)).forEach(line => {
        const tracked = trackedLines.find(tl => tl.lineId === line.lineId);
        const col = document.createElement('div');
        col.className = 'col-md-6 col-lg-4';
        
        let settingsHtml = '';
        if (tracked) {
            settingsHtml = `
                <div class="settings-panel mt-3">
                    <div class="form-check form-switch mb-3">
                        <input class="form-check-input" type="checkbox" id="notify-line-${line.lineId}" ${tracked.notify ? 'checked' : ''} onchange="updateLineSettings(${line.lineId})">
                        <label class="form-check-label" for="notify-line-${line.lineId}">Email Notifications</label>
                    </div>
                    <label class="form-label x-small text-muted mb-1">Minimum Alert Urgency</label>
                    <div class="d-flex align-items-center gap-2">
                        <select class="form-select form-select-sm" id="urgency-line-${line.lineId}" onchange="updateLineSettings(${line.lineId})">
                            ${urgencyLevels.map(u => `<option value="${u.val}" ${tracked.minUrgency === u.val ? 'selected' : ''}>${u.label}</option>`).join('')}
                        </select>
                        <span id="saved-line-${line.lineId}" class="text-success small fw-bold opacity-0" style="transition: opacity 0.3s; white-space: nowrap;"><i class="bi bi-check-circle-fill me-1"></i>Saved</span>
                    </div>
                </div>
            `;
        }

        col.innerHTML = `
            <div class="card h-100 shadow-sm tracking-card ${tracked ? 'active' : ''}">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-center">
                        <h5 class="card-title fw-bold mb-0">${line.name}</h5>
                        <button class="btn btn-sm ${tracked ? 'btn-danger' : 'btn-primary'}" 
                            onclick="toggleLine(${line.lineId}, ${!!tracked})">
                            ${tracked ? '<i class="bi bi-dash-circle me-1"></i>Untrack' : '<i class="bi bi-plus-circle me-1"></i>Track'}
                        </button>
                    </div>
                    ${settingsHtml}
                </div>
            </div>
        `;
        container.appendChild(col);
    });
}

async function updateLineSettings(lineId) {
    const notify = document.getElementById(`notify-line-${lineId}`).checked;
    const minUrgency = parseInt(document.getElementById(`urgency-line-${lineId}`).value);
    
    try {
        const res = await fetch('/api/tracking/lines', {
            method: 'PUT',
            headers: { ...getAuthHeader(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ lineId, notify, minUrgency })
        });
        if (res.ok) showSavedFeedback('line', lineId);
    } catch (err) { console.error(err); }
}

async function toggleLine(lineId, currentlyTracked) {
    const method = currentlyTracked ? 'DELETE' : 'POST';
    const url = currentlyTracked ? `/api/tracking/lines/${lineId}` : `/api/tracking/lines`;
    
    try {
        const res = await fetch(url, {
            method: method,
            headers: { ...getAuthHeader(), 'Content-Type': 'application/json' },
            body: currentlyTracked ? null : JSON.stringify({ lineId, notify: true, minUrgency: 2 })
        });
        if (res.ok) {
            const updatedTracked = await fetch('/api/tracking/lines', { headers: getAuthHeader() });
            trackedLines = await updatedTracked.json();
            renderLines();
        }
    } catch (err) { console.error(err); }
}

function renderStations() {
    const container = document.getElementById('stations-list');
    if (!container) return;
    const searchInput = document.getElementById('station-search');
    const search = searchInput ? searchInput.value.toLowerCase() : '';
    
    let displayStations = trackedStations.map(ts => {
        const s = allStations.find(as => as.stationId === ts.stationId);
        return s ? { ...s, isTracked: true, ...ts } : null;
    }).filter(s => s);

    if (search.length >= 2) {
        const searchResults = allStations
            .filter(s => s.commonName.toLowerCase().includes(search))
            .filter(s => !displayStations.some(ds => ds.stationId === s.stationId))
            .sort((a,b) => a.commonName.localeCompare(b.commonName))
            .slice(0, 20);
        displayStations = [...displayStations, ...searchResults];
    }

    if (displayStations.length === 0) {
        container.innerHTML = '<div class="col-12 text-center py-5 text-muted">' + (search.length < 2 ? 'Enter at least 2 characters to search...' : 'No stations found.') + '</div>';
        return;
    }

    container.innerHTML = '';
    displayStations.forEach(station => {
        const tracked = trackedStations.find(ts => ts.stationId === station.stationId);
        const col = document.createElement('div');
        col.className = 'col-md-6 col-lg-4';
        
        let settingsHtml = '';
        if (tracked) {
            settingsHtml = `
                <div class="settings-panel mt-3">
                    <div class="form-check form-switch mb-0 d-flex justify-content-between align-items-center">
                        <div>
                            <input class="form-check-input" type="checkbox" id="notify-station-${station.stationId}" ${tracked.notify ? 'checked' : ''} onchange="updateStationSettings(${station.stationId})">
                            <label class="form-check-label" for="notify-station-${station.stationId}">Email Notifications</label>
                        </div>
                        <span id="saved-station-${station.stationId}" class="text-success small fw-bold opacity-0" style="transition: opacity 0.3s; white-space: nowrap;"><i class="bi bi-check-circle-fill me-1"></i>Saved</span>
                    </div>
                </div>
            `;
        }

        col.innerHTML = `
            <div class="card h-100 shadow-sm tracking-card ${tracked ? 'active' : ''}">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-center mb-1">
                        <h6 class="card-title fw-bold mb-0">${station.commonName}</h6>
                        <button class="btn btn-sm ${tracked ? 'btn-danger' : 'btn-primary'}" 
                            onclick="toggleStation(${station.stationId}, ${!!tracked})">
                            ${tracked ? '<i class="bi bi-dash-circle me-1"></i>Untrack' : '<i class="bi bi-plus-circle me-1"></i>Track'}
                        </button>
                    </div>
                    <small class="text-muted d-block" style="font-size: 0.7rem;">${station.tflId}</small>
                    ${settingsHtml}
                </div>
            </div>
        `;
        container.appendChild(col);
    });
}

async function updateStationSettings(stationId) {
    const notify = document.getElementById(`notify-station-${stationId}`).checked;
    
    try {
        const res = await fetch('/api/tracking/stations', {
            method: 'PUT',
            headers: { ...getAuthHeader(), 'Content-Type': 'application/json' },
            body: JSON.stringify({ stationId, notify })
        });
        if (res.ok) showSavedFeedback('station', stationId);
    } catch (err) { console.error(err); }
}

async function toggleStation(stationId, currentlyTracked) {
    const method = currentlyTracked ? 'DELETE' : 'POST';
    const url = currentlyTracked ? `/api/tracking/stations/${stationId}` : `/api/tracking/stations`;
    
    try {
        const res = await fetch(url, {
            method: method,
            headers: { ...getAuthHeader(), 'Content-Type': 'application/json' },
            body: currentlyTracked ? null : JSON.stringify({ stationId, notify: true, minUrgency: 2 })
        });
        if (res.ok) {
            const updatedTracked = await fetch('/api/tracking/stations', { headers: getAuthHeader() });
            trackedStations = await updatedTracked.json();
            renderStations();
        }
    } catch (err) { console.error(err); }
}

async function verifyAccount() {
    const codeInput = document.getElementById('verify-code');
    const code = codeInput ? codeInput.value : '';
    const btn = document.getElementById('verify-btn');
    
    if (!code || code.length !== 6) {
        showVerifyAlert('Please enter a 6-digit code.', 'danger');
        return;
    }

    if (btn) btn.disabled = true;
    try {
        const res = await fetch('/api/user/verify', {
            method: 'POST',
            headers: { ...getAuthHeader(), 'Content-Type': 'application/json' },
            body: JSON.stringify(code)
        });
        const data = await res.json();
        
        if (res.ok) {
            showVerifyAlert('Account verified! Refreshing your session...', 'success');
            
            const refreshRes = await fetch('/api/auth/refresh', {
                method: 'POST',
                headers: getAuthHeader()
            });

            if (refreshRes.ok) {
                const refreshData = await refreshRes.json();
                const newToken = refreshData.token || refreshData.Token;
                if (newToken) {
                    localStorage.setItem('token', newToken);
                    setTimeout(() => window.location.reload(), 1500);
                    return;
                }
            }
            
            showVerifyAlert('Verified! Please log in again to continue.', 'success');
            setTimeout(() => logout(), 2000);
        } else {
            showVerifyAlert(data.message || 'Verification failed.', 'danger');
            if (btn) btn.disabled = false;
        }
    } catch (err) {
        showVerifyAlert('An error occurred.', 'danger');
        if (btn) btn.disabled = false;
    }
}

async function resendCode() {
    const link = document.getElementById('resend-link');
    const spinner = document.getElementById('resend-spinner');
    
    if (link) link.classList.add('d-none');
    if (spinner) spinner.classList.remove('d-none');
    
    try {
        const res = await fetch('/api/user/resend-verification', {
            method: 'POST',
            headers: getAuthHeader()
        });
        const data = await res.json();
        showVerifyAlert(data.message, res.ok ? 'success' : 'danger');
    } catch (err) {
        showVerifyAlert('Failed to resend code.', 'danger');
    } finally {
        if (link) link.classList.remove('d-none');
        if (spinner) spinner.classList.add('d-none');
    }
}

function showVerifyAlert(msg, type) {
    const alert = document.getElementById('verify-alert');
    if (alert) {
        alert.className = `alert alert-${type}`;
        alert.innerText = msg;
        alert.classList.remove('d-none');
    }
}

// Expose to window
window.verifyAccount = verifyAccount;
window.resendCode = resendCode;
window.toggleLine = toggleLine;
window.updateLineSettings = updateLineSettings;
window.toggleStation = toggleStation;
window.updateStationSettings = updateStationSettings;

async function refreshTokenIfOld() {
    const payload = getDecodedToken();
    if (!payload || !payload.iat) return;

    // Refresh if the token is older than 3 days
    const threeDaysInSeconds = 3 * 24 * 60 * 60;
    const now = Math.floor(Date.now() / 1000);
    
    if (now - payload.iat > threeDaysInSeconds) {
        console.log('Token is old, refreshing session...');
        try {
            const response = await fetch('/api/auth/refresh', {
                method: 'POST',
                headers: getAuthHeader()
            });
            if (response.ok) {
                const data = await response.json();
                const newToken = data.token || data.Token;
                if (newToken) {
                    localStorage.setItem('token', newToken);
                    console.log('Session refreshed successfully.');
                }
            }
        } catch (e) {
            console.error('Failed to refresh session:', e);
        }
    }
}

document.addEventListener('DOMContentLoaded', () => {
    updateNavbar();
    
    if (isLoggedIn()) {
        refreshTokenIfOld();

        // Handle Tracking Page
        if (document.getElementById('tracking-content')) {
            if (!isVerified()) {
                document.getElementById('tracking-content').classList.add('d-none');
                document.getElementById('unverified-prompt').classList.remove('d-none');
            } else {
                initTracking();
                
                const searchInput = document.getElementById('station-search');
                if (searchInput) {
                    searchInput.addEventListener('input', renderStations);
                }

                // Handle deep linking to tabs
                const hash = window.location.hash;
                if (hash === '#stations') {
                    const stationsTab = document.getElementById('stations-tab');
                    if (stationsTab) stationsTab.click();
                } else if (hash === '#lines') {
                    const linesTab = document.getElementById('lines-tab');
                    if (linesTab) linesTab.click();
                }
            }
        }

        // Handle Verification Banner
        const banner = document.getElementById('verification-banner');
        if (banner && !isVerified()) {
            banner.classList.remove('d-none');
        }

        // Handle Dashboard
        const trackedStatus = document.getElementById('tracked-status');
        if (trackedStatus) {
            trackedStatus.classList.remove('d-none');
            const statusTitle = document.getElementById('status-title');
            if (statusTitle) statusTitle.innerText = "All Line Statuses";
        }
    } else {
        const guestHero = document.getElementById('guest-hero');
        if (guestHero) guestHero.classList.remove('d-none');
        const guestFeatures = document.getElementById('guest-features');
        if (guestFeatures) guestFeatures.classList.remove('d-none');
        
        // Redirect to login if on tracking page and not logged in
        if (document.getElementById('tracking-content')) {
            window.location.href = '/login.html';
        }
    }

    let pollingInterval, timeAgoInterval;

    function refreshData() {
        const refreshingHtml = `<span class="text-primary"><span class="spinner-border spinner-border-sm me-1"></span>Refreshing...</span>`;
        const resEl = document.getElementById('api-result');
        const trackedResEl = document.getElementById('tracked-api-result');
        
        if (resEl && lastUpdateLineTime) resEl.innerHTML = refreshingHtml;
        if (trackedResEl && lastUpdateTrackedTime && isLoggedIn() && isVerified()) trackedResEl.innerHTML = refreshingHtml;

        loadTubeStatus();
        if (isLoggedIn() && document.getElementById('tracked-status')) {
            loadTrackedStatus();
        }
    }

    function startPolling() {
        stopPolling();
        refreshData();
        pollingInterval = setInterval(refreshData, 60000);
        timeAgoInterval = setInterval(() => {
            updateTimeAgo('api-result', lastUpdateLineTime);
            updateTimeAgo('tracked-api-result', lastUpdateTrackedTime);
        }, 1000);
    }

    function stopPolling() {
        if (pollingInterval) clearInterval(pollingInterval);
        if (timeAgoInterval) clearInterval(timeAgoInterval);
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) stopPolling();
        else startPolling();
    });

    // Start initial polling
    startPolling();
});