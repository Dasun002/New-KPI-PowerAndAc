/*
 File: region-management.component.ts
 Description: Region management and assignment page
 Purpose: Manage regional entities, assign network engineers, configure LEA codes.
 Features: CRUD operations, region listing, engineer assignment, data validation
*/

import { Inject, ChangeDetectorRef } from '@angular/core';

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { RegionService } from '../../../../services/region.service';

// Use the same interface as the service for consistency
export interface Region {
  id: number;
  region: string;
  province: string;
  networkengineer: string;
  engname: string;
  leacode: string;
}

type RegionKey = 'region' | 'province' | 'networkengineer' | 'engname' | 'leacode';


@Component({
  selector: 'app-region-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './region-management.component.html',
  styleUrls: ['./region-management.component.scss'],
})
export class RegionManagementComponent {
  // Injects the required dependencies.
  constructor(
    @Inject(RegionService) private regionService: RegionService,
    private cdr: ChangeDetectorRef
  ) {}
  
  pageTitle = 'Region Management';
  showForm = false;
  isSubmitting = false;
  error = '';
  success = '';
  
  // Form data with correct property names
  formData: Omit<Region, 'id'> = {
    region: '',
    province: '',
    networkengineer: '',
    engname: '',
    leacode: '',
  };
  
  regions: Region[] = [];
  sortKey: RegionKey | null = null;
  sortDirection: 'asc' | 'desc' = 'asc';
  editingRowId: number | null = null;
  editingField: RegionKey | null = null;
  editingValue = '';
  rowEdits: Partial<Region> = {};

  ngOnInit(): void {
    // Load the region list before enabling inline editing or new-record submission.
    this.loadRegions();
  }

  loadRegions(): void {
    // Normalize backend property aliases so older and newer API casing render identically.
    this.regionService.getAll().subscribe({
      next: (data: any[]) => {
        console.log('Raw data from service:', data); // Debug log
        
        // Map the data properly
        this.regions = data.map(item => {
          console.log('Processing item:', item); // Debug log
          
          // Handle different possible property names from backend
          return {
            id: item.id || item.ID || 0,
            region: item.region || item.Region || item.REGION || '',
            province: item.province || item.Province || item.PROVINCE || '',
            networkengineer: item.networkengineer || item.networkEngineer || 
                           item.NetworkEngineer || item.NETWORKENGINEER || 
                           item.ne || item.NE || '',
            engname: item.engname || item.EngName || item.engName || item.ENGNAME || item['Eng-Name'] || item['eng-name'] || '',
            leacode: item.leacode || item.leaCode || item.lea || 
                    item.LEA || item.Lea || item.LEACODE || ''
          };
        });
        
        console.log('Processed regions:', this.regions); // Debug log
        this.cdr.detectChanges();
      },
      error: (err: unknown) => {
        console.error('Error loading regions:', err);
        this.error = 'Failed to load region data';
        this.cdr.detectChanges();
      }
    });
  }

  /* ================= HEADER / FORM HELPERS ================= */

  openForm(): void {
    // Open a clean add form and clear messages from the previous operation.
    this.showForm = true;
    this.error = '';
    this.success = '';
  }

  closeForm(form?: NgForm): void {
    // Hide the form and reset both the model and any template-driven form state.
    this.showForm = false;
    this.error = '';
    this.success = '';
    this.resetForm(form);
  }

  resetForm(form?: NgForm): void {
    // Restore empty region fields and optionally reset Angular's form validation state.
    this.formData = {
      region: '',
      province: '',
      networkengineer: '',
      engname: '',
      leacode: '',
    };

    if (form) {
      form.resetForm();
    }
  }

  /* ================= ADD REGION (SUBMIT) ================= */

  onSubmit(form: NgForm): void {
    // Validate and trim all fields before creating a new region through the service.

    this.error = '';
    this.success = '';

    if (!form.valid) {
      this.error = 'Please fill all required fields.';
      form.control.markAllAsTouched();
      return;
    }

    const trimmed = {
      region: this.formData.region.trim(),
      province: this.formData.province.trim(),
      networkengineer: this.formData.networkengineer.trim(),
      engname: this.formData.engname.trim(),
      leacode: this.formData.leacode.trim(),
    };

    if (
      !trimmed.region ||
      !trimmed.province ||
      !trimmed.networkengineer ||
      !trimmed.engname ||
      !trimmed.leacode
    ) {
      this.error = 'All fields are required.';
      return;
    }

    this.isSubmitting = true;

    this.regionService.create({
      region: trimmed.region,
      province: trimmed.province,
      networkengineer: trimmed.networkengineer,
      engname: trimmed.engname,
      leacode: trimmed.leacode
    } as any).subscribe({
      next: (created: any) => {
        console.log('Created region:', created); // Debug log

        // Map the response to match our Region interface
        const newRegion: Region = {
          id: created.id || created.ID || this.regions.length + 1,
          region: created.region || created.Region || '',
          province: created.province || created.Province || '',
          networkengineer:
            created.networkengineer || created.networkEngineer || created.NetworkEngineer || '',
          engname:
            created.engname ||
            created.EngName ||
            created.engName ||
            created.ENGNAME ||
            created['Eng-Name'] ||
            created['eng-name'] ||
            '',
          leacode: created.leacode || created.leaCode || created.lea || ''
        };

        this.regions = [newRegion, ...this.regions];
        this.success = 'Region added successfully.';
        this.isSubmitting = false;
        this.closeForm(form);
        this.cdr.detectChanges();
      },
      error: (err: unknown) => {
        console.error('Error creating region:', err);
        this.error = 'Failed to add region.';
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  /* ================= SORTING ================= */

  requestSort(key: RegionKey): void {
    // Repeatedly selecting one column toggles its direction; a new column starts ascending.
    if (this.sortKey === key) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortKey = key;
      this.sortDirection = 'asc';
    }
  }

  getSortIndicator(key: RegionKey): string {
    if (this.sortKey !== key) return '';
    return this.sortDirection === 'asc' ? '▲' : '▼';
  }

  get sortedRegions(): Region[] {
    // Sort a copied array so changing the table order does not mutate the source collection.
    const data = [...this.regions];
    if (!this.sortKey) return data;

    const key = this.sortKey;
    const direction = this.sortDirection;

    return data.sort((a, b) => {
      const aVal = (a[key] ?? '').toString().toLowerCase();
      const bVal = (b[key] ?? '').toString().toLowerCase();

      if (aVal < bVal) return direction === 'asc' ? -1 : 1;
      if (aVal > bVal) return direction === 'asc' ? 1 : -1;
      return 0;
    });
  }

  /* ================= INLINE ROW EDITING ================= */

  isEditingRow(row: Region): boolean {
    return this.editingRowId === row.id;
  }

  startRowEdit(row: Region): void {
    // Snapshot the row into rowEdits so inline changes can be cancelled safely.
    this.editingRowId = row.id;
    this.rowEdits = { ...row };
  }

  cancelRowEdit(): void {
    // Discard the inline snapshot and leave all persisted row values unchanged.
    this.editingRowId = null;
    this.rowEdits = {};
  }

  saveRow(row: Region): void {
    // Merge edited values with unchanged fields, trim them, and persist the complete region row.
    const updateData = {
      id: row.id,
      region: (this.rowEdits['region'] ?? row.region).toString().trim(),
      province: (this.rowEdits['province'] ?? row.province).toString().trim(),
      networkengineer: (this.rowEdits['networkengineer'] ?? row.networkengineer).toString().trim(),
      engname: (this.rowEdits['engname'] ?? row.engname).toString().trim(),
      leacode: (this.rowEdits['leacode'] ?? row.leacode).toString().trim()
    };

    this.regionService.update(row.id, updateData).subscribe({
      next: (res: any) => {
        Object.assign(row, {
          region: res.region || res.Region || updateData.region,
          province: res.province || res.Province || updateData.province,
          networkengineer: res.networkengineer || res.networkEngineer || res.NetworkEngineer || updateData.networkengineer,
          engname: res.engname || res.EngName || res.engName || updateData.engname,
          leacode: res.leacode || res.leaCode || updateData.leacode
        });
        this.cancelRowEdit();
        this.cdr.detectChanges();
      },
      error: (err: unknown) => {
        console.error('Error updating region:', err);
        alert('Update failed');
        this.cdr.detectChanges();
      }
    });
  }

  /* ================= DELETE ROW ================= */

  handleDelete(id: number): void {
    // Confirm deletion, then remove the matching row locally after the API succeeds.
    if (!confirm('Are you sure you want to delete this region entry?')) return;

    this.regionService.delete(id).subscribe({
      next: () => {
        this.regions = this.regions.filter(r => r.id !== id);
        this.cdr.detectChanges();
      },
      error: (err: unknown) => {
        console.error('Error deleting region:', err);
        alert('Delete failed');
        this.cdr.detectChanges();
      }
    });
  } 
}
