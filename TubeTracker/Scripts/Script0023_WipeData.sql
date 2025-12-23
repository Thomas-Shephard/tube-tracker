-- Wipe all transactional and user-specific data
SET FOREIGN_KEY_CHECKS = 0;

TRUNCATE TABLE UserVerificationToken;
TRUNCATE TABLE PasswordResetToken;
TRUNCATE TABLE DeniedToken;
TRUNCATE TABLE TrackedLine;
TRUNCATE TABLE TrackedStation;
TRUNCATE TABLE LineStatusHistory;
TRUNCATE TABLE StationStatusHistory;
TRUNCATE TABLE User;

-- We also wipe metadata as it will be repopulated by background services
TRUNCATE TABLE Line;
TRUNCATE TABLE Station;

SET FOREIGN_KEY_CHECKS = 1;
