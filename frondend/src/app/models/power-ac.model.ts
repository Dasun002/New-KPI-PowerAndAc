export interface PowerAcKpi {
  id: number;
  section: 'Power' | 'AC';
  areaCode: string;
  unavailableMinutes: number | null;
  totalMinutes: number | null;
  totalSites: number | null;
  kpiPercent: number | null;
  month: number;
  year: number;
}