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

function formatSeverity(activeStatuses) {
    if (!activeStatuses || activeStatuses.length === 0) {
        return { display: "Good Service", full: "" };
    }

    // Sort by urgency descending (higher urgency first), then severityLevel ascending (lower number is more severe)
    const sorted = [...activeStatuses].sort((a, b) =>
        (b.severity.urgency - a.severity.urgency) || (a.severity.severityLevel - b.severity.severityLevel)
    );

    const descriptions = [...new Set(sorted.map(s => s.severity.description))];
    const fullList = descriptions.join(" & ");

    if (descriptions.length === 2) {
        return {
            display: `${descriptions[0]} & ${descriptions[1]}`,
            full: ""
        };
    } else if (descriptions.length > 2) {
        return {
            display: `${descriptions[0]} & More`,
            full: fullList
        };
    }
    
    return { display: descriptions[0], full: "" };
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

async function loadTubeStatus() {
    const resultElement = document.getElementById('api-result');
    const listContainer = document.getElementById('tube-list');
    if (!listContainer) return;

    resultElement.innerText = "Fetching latest data...";
    
    try {
        const response = await fetch('/api/status/lines');
        if (response.ok) {
            let lines = await response.json();
            resultElement.innerText = `Last updated: ${new Date().toLocaleTimeString()}`;
            listContainer.innerHTML = '';

            // Sort by severity then name
            lines = lines.map(line => ({
                ...line,
                minSeverityId: line.statuses && line.statuses.length > 0 
                    ? Math.min(...line.statuses.map(s => s.severity.severityLevel)) 
                    : 10
            })).sort((a, b) => (a.minSeverityId - b.minSeverityId) || a.name.localeCompare(b.name));

            lines.forEach(line => {
                const activeStatuses = line.statuses || [];
                const { display: severityDescription, full: fullStatus } = formatSeverity(activeStatuses);
                const reasons = [...new Set(activeStatuses.map(s => s.reason).filter(r => r))];
                
                let badgeClass = "bg-success";
                let statusClass = "status-good";
                if (line.minSeverityId < 10 && line.minSeverityId > 5) {
                    badgeClass = "bg-warning text-dark";
                    statusClass = "status-minor";
                } else if (line.minSeverityId <= 5) {
                    badgeClass = "bg-danger";
                    statusClass = "status-severe";
                }

                listContainer.insertAdjacentHTML('beforeend', createCardHtml(line.name, severityDescription, badgeClass, statusClass, reasons, false, fullStatus));
            });
            initTooltips();
        }
    } catch (e) { console.error(e); }
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

    try {
        const response = await fetch('/api/status/tracked', { headers: getAuthHeader() });
        if (response.ok) {
            const data = await response.json();
            resultElement.innerText = `Last updated: ${new Date().toLocaleTimeString()}`;
            
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
                const activeStatuses = line.statuses || [];
                const maxUrgency = activeStatuses.length ? Math.max(...activeStatuses.map(s => s.severity.urgency)) : 0;
                // MinUrgency defaults to 2 (Severe) if not set, or we can use the value from the object
                const isFlagged = maxUrgency >= (line.minUrgency ?? 2) && maxUrgency > 0;
                return { ...line, isFlagged, maxUrgency };
            }).sort((a, b) => {
                if (a.isFlagged && !b.isFlagged) return -1;
                if (!a.isFlagged && b.isFlagged) return 1;
                return (b.maxUrgency - a.maxUrgency) || a.name.localeCompare(b.name);
            });

            sortedLines.forEach(line => {
                const activeStatuses = line.statuses || [];
                const minSeverityId = activeStatuses.length ? Math.min(...activeStatuses.map(s => s.severity.severityLevel)) : 10;
                const { display: severityDescription, full: fullStatus } = formatSeverity(activeStatuses);
                const reasons = [...new Set(activeStatuses.map(s => s.reason).filter(r => r))];
                
                let badgeClass = minSeverityId < 10 ? (minSeverityId <= 5 ? "bg-danger" : "bg-warning text-dark") : "bg-success";
                let statusClass = minSeverityId < 10 ? (minSeverityId <= 5 ? "status-severe" : "status-minor") : "status-good";
                
                lineList.insertAdjacentHTML('beforeend', createCardHtml(line.name, severityDescription, badgeClass, statusClass, reasons, line.isFlagged, fullStatus));
            });

            const sortedStations = data.stations.map(station => {
                const activeStatuses = station.statuses || [];
                const isFlagged = activeStatuses.length > 0 && activeStatuses.some(s => s.statusDescription !== 'No Issues');
                return { ...station, isFlagged };
            }).sort((a, b) => {
                if (a.isFlagged && !b.isFlagged) return -1;
                if (!a.isFlagged && b.isFlagged) return 1;
                return a.commonName.localeCompare(b.commonName);
            });

            sortedStations.forEach(station => {
                const activeStatuses = station.statuses || [];
                const hasIssues = station.isFlagged;
                
                const badgeText = hasIssues ? "Disruption" : "No disruptions";
                const reasons = hasIssues ? activeStatuses.map(s => s.statusDescription) : [];
                
                let badgeClass = hasIssues ? "bg-warning text-dark" : "bg-success";
                let statusClass = hasIssues ? "status-minor" : "status-good";

                stationList.insertAdjacentHTML('beforeend', createCardHtml(station.commonName, badgeText, badgeClass, statusClass, reasons, hasIssues));
            });
            initTooltips();
        } else if (response.status === 401) {
            logout();
        }
    } catch (e) { console.error(e); }
}

function createCardHtml(name, severity, badgeClass, statusClass, reasons, isFlagged = false, fullStatus = "") {
    const bell = isFlagged ? '<i class="bi bi-bell-fill me-2" title="Matches your notification settings"></i>' : '';
    const infoIcon = fullStatus ? ` <i class="bi bi-info-circle-fill ms-1" data-bs-toggle="tooltip" data-bs-title="${fullStatus}" style="cursor: help;"></i>` : '';
    return `
        <div class="col-md-6 col-lg-4">
            <div class="card h-100 shadow-sm line-card ${statusClass} ${isFlagged ? 'flagged' : ''}">
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

function initTooltips() {
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));
}

// Global scope for onclick
window.logout = logout;
window.isLoggedIn = isLoggedIn;
window.isVerified = isVerified;
window.getAuthHeader = getAuthHeader;
window.updateNavbar = updateNavbar;

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
        const banner = document.getElementById('verification-banner');
        if (banner && !isVerified()) {
            banner.classList.remove('d-none');
        }

        document.getElementById('tracked-status').classList.remove('d-none');
        document.getElementById('status-title').innerText = "All Line Statuses";
        loadTrackedStatus();
        setInterval(loadTrackedStatus, 60000);
    } else {
        document.getElementById('guest-hero').classList.remove('d-none');
        document.getElementById('guest-features').classList.remove('d-none');
    }
    
    loadTubeStatus();
    setInterval(loadTubeStatus, 60000);
});