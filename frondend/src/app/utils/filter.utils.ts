// Centralizes the month/year options shared by dashboard, analytics, and platform forms.
export class FilterUtils {
  private static allMonths: Array<{ label: string; value: number }> = [
    { label: 'January', value: 1 },
    { label: 'February', value: 2 },
    { label: 'March', value: 3 },
    { label: 'April', value: 4 },
    { label: 'May', value: 5 },
    { label: 'June', value: 6 },
    { label: 'July', value: 7 },
    { label: 'August', value: 8 },
    { label: 'September', value: 9 },
    { label: 'October', value: 10 },
    { label: 'November', value: 11 },
    { label: 'December', value: 12 }
  ];

  static generateYearOptions(): number[] {
    // Overall pages expose years from the configured starting year through the current year.
    const currentYear = new Date().getFullYear();
    const years: number[] = [];
    for (let year = 2026; year <= currentYear; year++) {
      years.push(year);
    }
    return years;
  }

  static generatePlatformYearOptions(): number[] {
    // Platform pages expose a rolling current-year window, with the 2026 transition exception.
    const currentYear = new Date().getFullYear();
    const years: number[] = [];

    if (currentYear === 2026) {
      return [2026, 2027];
    }

    years.push(currentYear - 1, currentYear, currentYear + 1);
    return years;
  }

  static getMonthOptions(selectedYear?: number): Array<{ label: string; value: number }> {
    // Return months allowed for the selected year, hiding future months in the current year.
    const currentYear = new Date().getFullYear();
    const currentMonth = new Date().getMonth() + 1;

    // Platform rule: when the selected year is 2026, hide Jan/Feb/Mar.
    // Ensure this is applied even if caller passes string values.
    if (Number(selectedYear) === 2026) {
      return FilterUtils.allMonths.filter(m => m.value > 3);
    }

    if (selectedYear === currentYear) {
      return FilterUtils.allMonths.filter(m => m.value <= currentMonth);
    }

    return FilterUtils.allMonths;
  }
}
