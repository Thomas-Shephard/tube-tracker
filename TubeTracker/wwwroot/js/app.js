function isLoggedIn() {
    return localStorage.getItem('token') !== null;
}

function isVerified() {
    const token = localStorage.getItem('token');
    if (!token) return false;
    try {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(window.atob(base64).split('').map(function(c) {
            return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
        }).join(''));
        return JSON.parse(jsonPayload).is_verified === 'true';
    } catch (e) {
        return false;
    }
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
                const severityDescription = activeStatuses.length > 0 ? activeStatuses.map(s => s.severity.description).join(" & ") : "Good Service";
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

                listContainer.insertAdjacentHTML('beforeend', createCardHtml(line.name, severityDescription, badgeClass, statusClass, reasons));
            });
        }
    } catch (e) { console.error(e); }
}

async function loadTrackedStatus() {
    if (!isLoggedIn()) return;
    
    const lineList = document.getElementById('tracked-line-list');
    const stationList = document.getElementById('tracked-station-list');
    const resultElement = document.getElementById('tracked-api-result');
    if (!lineList) return;

    if (!isVerified()) {
        resultElement.innerText = "Verification required.";
        lineList.innerHTML = '<div class="col-12"><div class="alert alert-warning">Please <a href="/tracking.html">verify your account</a> to see your tracked lines and stations.</div></div>';
        stationList.innerHTML = '';
        return;
    }

    try {
        const response = await fetch('/api/status/tracked', { headers: getAuthHeader() });
        if (response.ok) {
            const data = await response.json();
            resultElement.innerText = `Last updated: ${new Date().toLocaleTimeString()}`;
            
            lineList.innerHTML = data.lines.length ? '' : '<div class="col-12 text-muted">You are not tracking any lines.</div>';
            stationList.innerHTML = data.stations.length ? '' : '<div class="col-12 text-muted">You are not tracking any stations.</div>';

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
                const severityDescription = activeStatuses.length ? activeStatuses.map(s => s.severity.description).join(" & ") : "Good Service";
                const reasons = [...new Set(activeStatuses.map(s => s.reason).filter(r => r))];
                
                let badgeClass = minSeverityId < 10 ? (minSeverityId <= 5 ? "bg-danger" : "bg-warning text-dark") : "bg-success";
                let statusClass = minSeverityId < 10 ? (minSeverityId <= 5 ? "status-severe" : "status-minor") : "status-good";
                
                lineList.insertAdjacentHTML('beforeend', createCardHtml(line.name, severityDescription, badgeClass, statusClass, reasons, line.isFlagged));
            });

            data.stations.forEach(station => {
                const activeStatuses = station.statuses || [];
                const severityDescription = activeStatuses.length ? activeStatuses.map(s => s.statusDescription).join(" & ") : "No disruptions";
                let badgeClass = activeStatuses.length ? "bg-warning text-dark" : "bg-success";
                let statusClass = activeStatuses.length ? "status-minor" : "status-good";

                stationList.insertAdjacentHTML('beforeend', createCardHtml(station.commonName, severityDescription, badgeClass, statusClass, []));
            });
        } else if (response.status === 401) {
            logout();
        }
    } catch (e) { console.error(e); }
}

function createCardHtml(name, severity, badgeClass, statusClass, reasons, isFlagged = false) {
    const bell = isFlagged ? '<i class="bi bi-bell-fill me-2" title="Matches your notification settings"></i>' : '';
    return `
        <div class="col-md-6 col-lg-4">
            <div class="card h-100 shadow-sm line-card ${statusClass} ${isFlagged ? 'flagged' : ''}">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <h5 class="card-title fw-bold mb-0">${bell}${name}</h5>
                        <span class="badge ${badgeClass}">${severity}</span>
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

document.addEventListener('DOMContentLoaded', () => {
    updateNavbar();
    
    if (isLoggedIn()) {
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