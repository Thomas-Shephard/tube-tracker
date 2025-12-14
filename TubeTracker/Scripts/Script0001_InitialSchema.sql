CREATE TABLE User
(
    user_id       INT AUTO_INCREMENT PRIMARY KEY,
    email         VARCHAR(255) UNIQUE NOT NULL,
    name          VARCHAR(70)         NOT NULL,
    password_hash VARCHAR(511)        NOT NULL,
    last_login    TIMESTAMP DEFAULT NULL,
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

CREATE TABLE DeniedToken
(
    jti        VARCHAR(36) PRIMARY KEY,
    expires_at TIMESTAMP NOT NULL,
    INDEX idx_expires_at (expires_at)
);

CREATE TABLE PasswordResetToken
(
    token_id   INT AUTO_INCREMENT PRIMARY KEY,
    user_id    INT          NOT NULL,
    token_hash VARCHAR(511) NOT NULL,
    expiration TIMESTAMP    NOT NULL DEFAULT (CURRENT_TIMESTAMP + INTERVAL 15 MINUTE),
    is_used    BOOL         NOT NULL DEFAULT FALSE,
    is_revoked BOOL         NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP             DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES User (user_id) ON DELETE CASCADE
);

CREATE TABLE StatusSeverity
(
    severity_level INT PRIMARY KEY,
    description    VARCHAR(50) NOT NULL,
    urgency        INT DEFAULT 0 -- 0=Good, 1=Minor, 2=Severe, 3=Critical
);

INSERT INTO StatusSeverity (severity_level, description, urgency)
VALUES (0, 'Special Service', 1),
       (1, 'Closed', 3),
       (2, 'Suspended', 3),
       (3, 'Part Suspended', 3),
       (4, 'Planned Closure', 2),
       (5, 'Part Closure', 2),
       (6, 'Severe Delays', 2),
       (7, 'Reduced Service', 1),
       (8, 'Bus Service', 2),
       (9, 'Minor Delays', 1),
       (10, 'Good Service', 0),
       (11, 'Part Closed', 3),
       (12, 'Exit Only', 1),
       (13, 'No Step Free Access', 1),
       (14, 'Change of frequency', 1),
       (15, 'Diverted', 1),
       (16, 'Not Running', 3),
       (17, 'Issues Reported', 1),
       (18, 'No Issues', 0),
       (19, 'Information', 0),
       (20, 'Service Closed', 3);

CREATE TABLE Line
(
    line_id   INT AUTO_INCREMENT PRIMARY KEY,
    tfl_id    VARCHAR(50) UNIQUE NOT NULL,
    name      VARCHAR(100)       NOT NULL,
    mode_name VARCHAR(50)        NOT NULL,
    colour    VARCHAR(7)
);

CREATE TABLE TrackedLine
(
    tracked_line_id  INT AUTO_INCREMENT PRIMARY KEY,
    user_id          INT       NOT NULL,
    line_id          INT       NOT NULL,
    notify           BOOL,
    min_urgency      INT,
    last_notified_at TIMESTAMP NULL,
    created_at       TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY unique_user_line (user_id, line_id),
    FOREIGN KEY (user_id) REFERENCES User (user_id) ON DELETE CASCADE,
    FOREIGN KEY (line_id) REFERENCES Line (line_id) ON DELETE CASCADE
);

CREATE TABLE LineStatusHistory
(
    history_id         INT AUTO_INCREMENT PRIMARY KEY,
    line_id            INT          NOT NULL,
    status_severity    INT          NOT NULL,
    status_description VARCHAR(255) NOT NULL,
    checked_at         TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_line_time (line_id, checked_at),
    FOREIGN KEY (line_id) REFERENCES Line (line_id) ON DELETE CASCADE
);

CREATE TABLE Station
(
    station_id  INT AUTO_INCREMENT PRIMARY KEY,
    tfl_id      VARCHAR(50) UNIQUE NOT NULL,
    common_name VARCHAR(255)       NOT NULL,
    lat         DOUBLE,
    lon         DOUBLE
);

CREATE TABLE TrackedStation
(
    tracked_station_id   INT AUTO_INCREMENT PRIMARY KEY,
    user_id              INT       NOT NULL,
    station_id           INT       NOT NULL,
    notify               BOOL,
    notify_accessibility BOOL,
    min_urgency          INT,
    last_notified_at     TIMESTAMP NULL,
    created_at           TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY unique_user_station (user_id, station_id),
    FOREIGN KEY (user_id) REFERENCES User (user_id) ON DELETE CASCADE,
    FOREIGN KEY (station_id) REFERENCES Station (station_id) ON DELETE CASCADE
);

CREATE TABLE StationStatusHistory
(
    history_id         INT AUTO_INCREMENT PRIMARY KEY,
    station_id         INT  NOT NULL,
    status_description TEXT NOT NULL,
    checked_at         TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_station_time (station_id, checked_at),
    FOREIGN KEY (station_id) REFERENCES Station (station_id) ON DELETE CASCADE
);
