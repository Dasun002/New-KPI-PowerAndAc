import { buildAreaLabels, getScopedLeas } from './region-display.utils';

describe('region-display utils', () => {
  const rows = [
    { region: 'R1', province: 'P1', networkEngineer: 'E1', lea: 'AD/PR' },
    { region: 'R1', province: 'P1', networkEngineer: 'E1', lea: 'AD/PR' },
    { region: 'R1', province: 'P2', networkEngineer: 'E2', lea: 'JA' },
    { region: 'R2', province: 'P3', networkEngineer: 'E3', lea: ' ' }
  ];

  it('getScopedLeas returns distinct LEAs for each filter level, blank levels matching all', () => {
    expect(getScopedLeas(rows, {})).toEqual(['AD/PR', 'JA']);
    expect(getScopedLeas(rows, { region: 'R1', province: 'P2' })).toEqual(['JA']);
    expect(getScopedLeas(rows, { engineer: 'E1' })).toEqual(['AD/PR']);
    expect(getScopedLeas(rows, { region: 'R2' })).toEqual([]);
  });

  it('buildAreaLabels only appends the key when labels collide', () => {
    const mapping: Record<string, string> = { cenhkmd: 'CEN/HK/MD', cenhkmd1: 'CEN/HK/MD', ja: 'JA' };
    expect(buildAreaLabels(['cenhkmd', 'cenhkmd1', 'ja'], key => mapping[key])).toEqual({
      cenhkmd: 'CEN/HK/MD (CENHKMD)',
      cenhkmd1: 'CEN/HK/MD (CENHKMD1)',
      ja: 'JA'
    });
  });
});
