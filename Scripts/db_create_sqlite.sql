-- Create the database (optional, SQLite creates the database file automatically when connecting)
-- PRAGMA commands can be used to configure SQLite settings if needed.

-- Create the SwitchHisEntity table
CREATE TABLE IF NOT EXISTS switch_rpt_his (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, -- Auto-incrementing ID
    SwitchNo TEXT NOT NULL,               -- Switch number
    MinTime DATETIME NOT NULL,            -- Earliest experiment time
    MaxTime DATETIME NOT NULL,            -- Latest experiment time
    CreateTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, -- Creation time
    RptJson TEXT                          -- JSON serialized report information
);

-- Index for faster lookups on SwitchNo
CREATE INDEX IF NOT EXISTS idx_SwitchNo ON switch_rpt_his (SwitchNo);