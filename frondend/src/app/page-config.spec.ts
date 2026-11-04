import { adminNavOptions, platformNavOptions } from './page-config';
import { routes } from './app.routes';

describe('Power & A/C platform navigation', () => {
  it('shows the KPI page without the legacy Power & A/C page', () => {
    expect(platformNavOptions.some(option => option.path === 'platform/power-ac')).toBe(false);
    expect(platformNavOptions.some(option => option.path === 'platform/power-ac-kpi')).toBe(true);
  });

  it('redirects legacy Power & A/C page links to the KPI page', () => {
    expect(routes.find(route => route.path === 'platform/power-ac')).toMatchObject({
      redirectTo: 'platform/power-ac-kpi',
      pathMatch: 'full'
    });
  });

  describe('Power & A/C admin navigation', () => {
    it('shows the configuration page without the legacy Power & A/C page', () => {
      expect(adminNavOptions.some(option => option.path === 'admin/power-ac')).toBe(false);
      expect(adminNavOptions.some(option => option.path === 'admin/power-ac-config')).toBe(true);
      expect(routes.find(route => route.path === 'admin/power-ac')).toMatchObject({
        redirectTo: 'admin/power-ac-config',
        pathMatch: 'full'
      });
    });
  });
});
