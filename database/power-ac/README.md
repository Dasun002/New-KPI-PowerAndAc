# Power & A/C – Database setup

This folder has three update scripts. Run them in order, one after the other.

| Order | Script | What it does |
|---|---|---|
| 1 | `006_power_ac_single_table_deploy.sql` | Creates the new Power & A/C data table, copies the existing Power & A/C rows into it, and sets up the four Power & A/C KPIs. It can be run again safely and it deletes nothing. |
| 2 | `007_drop_legacy_power_ac_tables.sql` | Removes the old Power & A/C tables. It only runs after step 1 has finished. If any old row is missing from the new table, it stops and deletes nothing. |
| 3 | `008_power_ac_safety_availability_inputs.sql` | Adds two input columns: the Critical Safety incident count and the Power System Availability %. It changes no existing value and can be run again safely. |

## Before you start

1. **Make a full backup of the database.** Step 2 cannot be undone.
2. Make sure you have the database server name, the database name (normally `NWKPI`), and a login that is allowed to change the database.

## Steps

### Step 1 – Run script 006

Run this from a command prompt in the project folder, replacing the placeholders in angle brackets:

```
sqlcmd -S <server> -U <user> -P <password> -C -d NWKPI -i database/power-ac/006_power_ac_single_table_deploy.sql
```

When it finishes, it shows two numbers:

- `power_ac_rows` – the number of Power & A/C rows in the new table. It should match the number of rows in the old table.
- `power_ac_kpis` – should be **4**.

### Step 2 – Run script 007

Only run this after step 1 has finished and the numbers look correct:

```
sqlcmd -S <server> -U <user> -P <password> -C -d NWKPI -i database/power-ac/007_drop_legacy_power_ac_tables.sql
```

When it finishes, the only Power & A/C table left should be `PowerAcPmData`.

### Step 3 – Run script 008

```
sqlcmd -S <server> -U <user> -P <password> -C -d NWKPI -i database/power-ac/008_power_ac_safety_availability_inputs.sql
```

It adds two columns to `PowerAcPmData` and reports how many rows already have a value. Existing Safety and Availability percentages are kept as they are.

## Important

- Do not run any other Power & A/C script. Earlier scripts have been removed.
- Do not write passwords into this document or into any shared file.
