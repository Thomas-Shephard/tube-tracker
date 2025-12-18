async function loadTubeStatus() {
    const resultElement = document.getElementById('api-result');
    const listContainer = document.getElementById('tube-list');
    
    resultElement.innerText = "Fetching latest data...";
    resultElement.className = "text-muted";

    try {
        const response = await fetch('/api/status/lines');
        if (response.ok) {
            const lines = await response.json();
            
            resultElement.innerText = `Last updated: ${new Date().toLocaleTimeString()}`;
            resultElement.className = "text-success mb-0";
            
            listContainer.innerHTML = ''; // clear list

            lines.forEach(line => {
                // The API returns an array of statuses, we take the most severe one or just the first one if it's active
                // For simplicity, we'll look at the first status in the 'statuses' array
                const status = (line.statuses && line.statuses.length > 0) 
                    ? line.statuses[0] 
                    : null;
                
                const severity = status ? status.severity.description : "Good Service";
                const severityId = status ? status.severity.severityLevel : 10;
                
                // Determine CSS class based on severityId (TfL standard: 10 is Good Service)
                let statusClass = "status-good";
                let badgeClass = "bg-success";
                
                if (severityId < 10 && severityId > 5) {
                    statusClass = "status-minor";
                    badgeClass = "bg-warning text-dark";
                } else if (severityId <= 5) {
                    statusClass = "status-severe";
                    badgeClass = "bg-danger";
                }

                const cardHtml = `
                    <div class="col-md-6 col-lg-4">
                        <div class="card h-100 shadow-sm line-card ${statusClass}">
                            <div class="card-body">
                                <div class="d-flex justify-content-between align-items-start mb-2">
                                    <h5 class="card-title fw-bold mb-0">${line.name}</h5>
                                    <span class="badge ${badgeClass}">${severity}</span>
                                </div>
                                ${status && status.reason ? `<p class="card-text small text-muted mt-2">${status.reason}</p>` : ''}
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

// Load on startup
document.addEventListener('DOMContentLoaded', loadTubeStatus);
