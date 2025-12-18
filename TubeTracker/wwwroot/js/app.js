async function loadTubeStatus() {
    const resultElement = document.getElementById('api-result');
    const listContainer = document.getElementById('tube-list');
    
    resultElement.innerText = "Fetching latest data...";
    resultElement.className = "text-muted";

    try {
        const response = await fetch('/api/status/lines');
        if (response.ok) {
            let lines = await response.json();
            
            resultElement.innerText = `Last updated: ${new Date().toLocaleTimeString()}`;
            resultElement.className = "text-success mb-0";
            
            listContainer.innerHTML = ''; // clear list

            // Pre-process lines to determine their minSeverityId for sorting
            lines = lines.map(line => {
                const activeStatuses = (line.statuses && line.statuses.length > 0) ? line.statuses : [];
                const minSeverityId = activeStatuses.length > 0 
                    ? Math.min(...activeStatuses.map(s => s.severity.severityLevel)) 
                    : 10;
                return { ...line, minSeverityId };
            });

            // Sort: 1. Severity (ascending ID, so 0-5 before 10) 2. Name (alphabetical)
            lines.sort((a, b) => {
                if (a.minSeverityId !== b.minSeverityId) {
                    return a.minSeverityId - b.minSeverityId;
                }
                return a.name.localeCompare(b.name);
            });

            lines.forEach(line => {
                const activeStatuses = (line.statuses && line.statuses.length > 0) ? line.statuses : [];
                
                let severityDescription = "Good Service";
                let minSeverityId = 10;
                let reasons = [];

                if (activeStatuses.length > 0) {
                    // Sort by severity level (lower is more severe)
                    activeStatuses.sort((a, b) => a.severity.severityLevel - b.severity.severityLevel);
                    
                    // Join descriptions: e.g. "Part Closure & Minor Delays"
                    severityDescription = activeStatuses.map(s => s.severity.description).join(" & ");
                    minSeverityId = activeStatuses[0].severity.severityLevel;
                    
                    // Collect unique reasons
                    reasons = activeStatuses
                        .map(s => s.reason)
                        .filter((reason, index, self) => reason && self.indexOf(reason) === index);
                }
                
                // Determine CSS class based on minSeverityId (TfL standard: 10 is Good Service)
                let statusClass = "status-good";
                let badgeClass = "bg-success";
                
                if (minSeverityId < 10 && minSeverityId > 5) {
                    statusClass = "status-minor";
                    badgeClass = "bg-warning text-dark";
                } else if (minSeverityId <= 5) {
                    statusClass = "status-severe";
                    badgeClass = "bg-danger";
                }

                const cardHtml = `
                    <div class="col-md-6 col-lg-4">
                        <div class="card h-100 shadow-sm line-card ${statusClass}">
                            <div class="card-body">
                                <div class="d-flex justify-content-between align-items-start mb-2">
                                    <h5 class="card-title fw-bold mb-0">${line.name}</h5>
                                    <span class="badge ${badgeClass}">${severityDescription}</span>
                                </div>
                                ${reasons.length > 0 ? `<div class="mt-2">${reasons.map(r => `<p class="card-text small text-muted mb-1">${r}</p>`).join('')}</div>` : ''}
                            </div>
                        </div>
                    </div>
                `;
                listContainer.insertAdjacentHTML('beforeend', cardHtml);
            });
            
        } else {
            resultElement.innerText = "Error loading data: " + response.statusText;
            resultElement.className = "text-danger";
            listContainer.innerHTML = `<div class="col-12"><div class="alert alert-danger">Failed to load tube status. Please try again later.</div></div>`;
        }
    } catch (error) {
        console.error("Fetch error:", error);
        resultElement.innerText = "Connection error. Please check your internet.";
        resultElement.className = "text-danger";
        listContainer.innerHTML = `<div class="col-12"><div class="alert alert-danger">An error occurred while connecting to the server.</div></div>`;
    }
}

// Load on startup and set interval
document.addEventListener('DOMContentLoaded', () => {
    loadTubeStatus();
    // Refresh every minute
    setInterval(loadTubeStatus, 60000);
});
