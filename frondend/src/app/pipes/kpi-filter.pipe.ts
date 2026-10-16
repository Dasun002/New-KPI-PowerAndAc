/* File: kpi-filter.pipe.ts
   Description: KPI row filtering pipe
   Purpose: Filters out specific rows from KPI arrays by row number.
   Usage: data | kpiFilter: [1, 2, 3]
*/

import { Pipe, PipeTransform } from '@angular/core';

/* ========== DATA INTERFACES ========== */

/* KPI row structure */
export interface KpiRow {
  /* Row sequence number */
  rowNumber?: number | null;
  /* Business perspectives */
  perspectives?: string | null;
  /* Strategic objectives */
  strategicObjectives?: string | null;
  /* Key Performance Indicator description */
  keyPerformanceIndicators?: string | null;
  /* Measurement unit */
  unit?: string | null;
  /* Detailed KPI description */
  descriptionOfKPI?: string | null;
  /* Weight in overall calculation */
  weightage?: number | null;
}

/* ========== KPI FILTER PIPE ========== */

@Pipe({
  name: 'kpiFilter',
  standalone: true,
  pure: true
})
export class KpiFilterPipe implements PipeTransform {
  /* Transform - filters out excluded row numbers */
  transform(
    rows: KpiRow[] | null | undefined,
    /* Row numbers to exclude from result */
    excludedRowNumbers: number[] = []
  ): KpiRow[] {
    /* Return empty array if input is not an array */
    if (!Array.isArray(rows)) {
      return [];
    }
    /* Return all rows if no exclusions specified */
    if (!excludedRowNumbers || excludedRowNumbers.length === 0) {
      return rows;
    }

    /* Filter out rows with excluded row numbers */
    return rows.filter(row => {
      const num = row.rowNumber != null ? Number(row.rowNumber) : NaN;
      return !excludedRowNumbers.includes(num);
    });
  }
}
