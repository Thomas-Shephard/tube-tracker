ALTER TABLE StationStatusSeverity DROP COLUMN is_accessibility;

DELETE FROM StationStatusHistory;
DELETE FROM StationStatusSeverity;

INSERT INTO StationStatusSeverity (description, urgency)
VALUES 
('No Disruptions', 0),
('Information', 0),
('Accessibility Issue', 1),
('Other', 1),
('Partially Closed', 2),
('Closed', 3);