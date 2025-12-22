CREATE TABLE StationStatusSeverity
(
    severity_id      INT AUTO_INCREMENT PRIMARY KEY,
    description      VARCHAR(50) UNIQUE NOT NULL,
    urgency          INT DEFAULT 0,
    is_accessibility BOOL DEFAULT FALSE
);

INSERT INTO StationStatusSeverity (description, urgency, is_accessibility)
VALUES ('Signal Failure', 2, 0),
       ('Train Fault', 2, 0),
       ('Passenger Incident', 2, 0),
       ('Staff Shortage', 2, 0),
       ('Strike Action', 3, 0),
       ('Weather Issue', 2, 0),
       ('Engineering Work', 2, 0),
       ('Station Closed', 3, 0),
       ('Fire Alert', 3, 0),
       ('Lift Fault', 1, 1),
       ('Escalator Fault', 1, 1),
       ('Other', 1, 0),          -- Changed from 0 to 1 (Minor)
       ('Good Service', 0, 0);   -- New category for "No Issues"

ALTER TABLE StationStatusHistory
    ADD COLUMN status_severity_id INT DEFAULT 12, -- Default to 'Other'
    ADD CONSTRAINT fk_station_status_severity FOREIGN KEY (status_severity_id) REFERENCES StationStatusSeverity (severity_id);