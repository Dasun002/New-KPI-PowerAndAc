# Power & A/C: steps after pulling feature/power-ac-kpi

The Power & A/C KPI now uses one table (dbo.PowerAcPmData) and its KPIs are rows in dbo.KpiDefinition.
Scripts 000 to 005 are obsolete. Do not run them.

## 1. Database (run on your local NWKPI database, in this order)
1. Back up your database.
2. `database/power-ac/006_power_ac_single_table_deploy.sql`  (creates and fills the new structure; safe to re-run)
3. `database/power-ac/007_drop_legacy_power_ac_tables.sql`   (removes the old Power & A/C tables)

Example with sqlcmd:
    sqlcmd -S <server> -U <user> -P <password> -C -d NWKPI -i database/power-ac/006_power_ac_single_table_deploy.sql
    sqlcmd -S <server> -U <user> -P <password> -C -d NWKPI -i database/power-ac/007_drop_legacy_power_ac_tables.sql

Expected after 006: power_ac_rows = your PM rows, power_ac_kpis = 4.
Expected after 007: the only Power & A/C table left is PowerAcPmData.

## 2. Connection string
Set your own connection string. Do not commit it. Options:
- Environment variable (per session):
    ConnectionStrings__DefaultConnection="Server=<server>;Database=NWKPI;User Id=<user>;Password=<password>;Encrypt=False;TrustServerCertificate=True"
- Or edit your local appsettings.Development.json (do not commit the password).

## 3. Build and run
- Backend: cd backend && dotnet run          (http://localhost:5043)
- Frontend: cd frondend && npm install && npx ng serve --port 4200

## 4. Check
- Log in, open Platform KPI > Power & A/C KPI: the overview and the two input grids should show.
- Admin > Power & A/C KPI Config: the four KPIs should be listed, each ticked "Scored".
- Enter Achievement % values, have another verifier verify them, then Calculate.

## Notes
- New KPIs are "Scored" by default. A scored KPI needs a value for every group and weights must total 100%.
- Power & A/C users need the Power & A/C page permission (User Management).
