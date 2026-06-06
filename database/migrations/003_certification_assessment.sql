-- Structured certification assessment + buyer-facing inspection report storage.
-- Safe to run on existing databases (additive columns only).

ALTER TABLE device_reports
    ADD COLUMN IF NOT EXISTS certification_assessment_json JSONB,
    ADD COLUMN IF NOT EXISTS inspection_report_json JSONB,
    ADD COLUMN IF NOT EXISTS assessment_version TEXT,
    ADD COLUMN IF NOT EXISTS resale_grade TEXT,
    ADD COLUMN IF NOT EXISTS overall_score NUMERIC(5, 2),
    ADD COLUMN IF NOT EXISTS battery_wear_percent NUMERIC(5, 2);

CREATE INDEX IF NOT EXISTS idx_device_reports_resale_grade
    ON device_reports(resale_grade);

ALTER TABLE scan_reports
    ADD COLUMN IF NOT EXISTS certification_assessment_json JSONB,
    ADD COLUMN IF NOT EXISTS inspection_report_json JSONB;
