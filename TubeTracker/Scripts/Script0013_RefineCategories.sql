INSERT IGNORE INTO StationStatusSeverity (description, urgency, is_accessibility)
VALUES ('No Step Free Access', 1, 1),
       ('Partially Closed', 2, 0);

UPDATE StationStatusSeverity SET urgency = 1 WHERE description = 'Staff Shortage';
