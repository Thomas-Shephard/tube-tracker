ALTER TABLE LineStatusHistory CHANGE COLUMN checked_at first_checked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE LineStatusHistory ADD COLUMN last_checked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
CREATE INDEX idx_line_status_last_check ON LineStatusHistory (line_id, status_severity, last_checked_at);

ALTER TABLE StationStatusHistory CHANGE COLUMN checked_at first_checked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE StationStatusHistory ADD COLUMN last_checked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
CREATE INDEX idx_station_status_last_check ON StationStatusHistory (station_id, last_checked_at);
