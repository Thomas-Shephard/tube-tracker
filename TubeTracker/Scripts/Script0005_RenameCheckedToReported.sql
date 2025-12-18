ALTER TABLE LineStatusHistory RENAME COLUMN first_checked_at TO first_reported_at;
ALTER TABLE LineStatusHistory RENAME COLUMN last_checked_at TO last_reported_at;

ALTER TABLE StationStatusHistory RENAME COLUMN first_checked_at TO first_reported_at;
ALTER TABLE StationStatusHistory RENAME COLUMN last_checked_at TO last_reported_at;
