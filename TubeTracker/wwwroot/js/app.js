async function loadTubeStatus() {
    const container = document.getElementById('status-container');
    const resultElement = document.getElementById('api-result');
    
    resultElement.innerText = "Loading tube status...";
    resultElement.className = "text-muted";
    
    // Clear previous results except the button/message area if desired, 
    // but here we will append a list below.
    const listContainer = document.getElementById('tube-list');
    listContainer.innerHTML = ''; // clear list

    try {
        const response = await fetch('/api/tube/status');
        if (response.ok) {
            const lines = await response.json();
            
            resultElement.innerText = `Updated: ${new Date().toLocaleTimeString()}`;
            resultElement.className = "text-success mb-3";

            lines.forEach(line => {
                const status = line.lineStatuses[0];
                const severity = status.statusSeverityDescription;
                
                // Determine color based on severity
                let badgeClass = "bg-success";
                if (severity !== "Good Service") {
                    badgeClass = "bg-warning text-dark";
                }
                if (severity.includes("Closed") || severity.includes("Suspended")) {
                    badgeClass = "bg-danger";
                }

                const cardHtml = `
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="card shadow-sm">
                            <div class="card-body d-flex justify-content-between align-items-center">
                                <h5 class="card-title mb-0">${line.name}</h5>
                                <span class="badge ${badgeClass}">${severity}</span>
                            </div>
                        </div>
                    </div>
                `;
                listContainer.insertAdjacentHTML('beforeend', cardHtml);
            });
            
        } else {
            resultElement.innerText = "Error loading data: " + response.statusText;
            resultElement.className = "text-danger";
        }
    } catch (error) {
        resultElement.innerText = "Fetch error: " + error;
        resultElement.className = "text-danger";
    }
}

// Load on startup
document.addEventListener('DOMContentLoaded', loadTubeStatus);