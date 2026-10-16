import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-analytics-button',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './analytics-button.component.html',
  styleUrls: ['./analytics-button.component.scss']
})
export class AnalyticsButtonComponent {}
