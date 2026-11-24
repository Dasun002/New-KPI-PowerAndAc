# NW KPI Monitoring System – Power & A/C

## What this system is

This system tracks the performance indicators (KPIs) for the network team. It records monthly results, calculates each team's score, and shows the results on screen for managers and reviewers.

The Power & A/C part of the system covers four measures for each engineer group:

- **Preventive Maintenance** – how much of the planned maintenance was completed.
- **Critical Safety** – the number of safety incidents.
- **Corrective Action** – how well faults and maintenance records were closed.
- **Power System Availability** – how often the power system was available.

## What is in this folder

| Folder or file | What it is |
|---|---|
| `backend/` | The server side of the system. It stores the data and performs the calculations. |
| `frondend/` | The screens people see and use in the web browser. |
| `database/power-ac/` | The database update scripts for the Power & A/C part, with setup instructions. |

## Setting up the database

Follow the steps in [database/power-ac/README.md](database/power-ac/README.md). In short:

1. Make a full backup of the database.
2. Run script `006`, then script `007`.

## Starting the system

The backend and frontend are standard projects. They must be built and started by a technical person, who can follow the notes in each folder. The database connection must be set up locally, and the password must never be saved in shared files.

## Important notes

- Only the Power & A/C scripts `006` and `007` are needed for this release. Earlier scripts have been removed because they are no longer used.
- Do not share database passwords or connection strings in this folder.
