import { Component, Input, TemplateRef } from '@angular/core';
// FIX: Import the specific directive we need
import { NgTemplateOutlet } from '@angular/common';

@Component({
  selector: 'app-card',
  standalone: true,
  // FIX: Add NgTemplateOutlet to the imports array
  imports: [NgTemplateOutlet],
  template: `
    <div class="card">
      @if (headerTemplate || header) {
        <div class="card-header">
          @if (headerTemplate) {
            <!-- This line will now work correctly -->
            <ng-container [ngTemplateOutlet]="headerTemplate"></ng-container>
          } @else {
            <h3 class="card-title">{{ header }}</h3>
          }
        </div>
      }

      <div class="card-body">
        <ng-content></ng-content>
      </div>

      @if (footerTemplate || footer) {
        <div class="card-footer">
          @if (footerTemplate) {
            <!-- This line will also work correctly -->
            <ng-container [ngTemplateOutlet]="footerTemplate"></ng-container>
          } @else {
            <span>{{ footer }}</span>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      /* Define the component's public CSS API using variables */
      --card-bg: var(--background-color-white, white);
      --card-border-color: var(--card-border-color, #eee);
      --card-header-footer-bg: var(--background-color-light, #f8f9fa);
      --card-shadow: var(--box-shadow, 0 2px 8px rgba(0, 0, 0, 0.1));
      --card-border-radius: 8px;
      --card-padding: 1.5rem;
      --card-header-footer-padding: 1rem 1.5rem;
      --card-title-color: var(--text-color-dark, #333);
      --card-title-font-size: 1.25rem;

      /* Ensure the component behaves as a block-level element */
      display: block;
    }

    .card {
      background: var(--card-bg);
      border-radius: var(--card-border-radius);
      box-shadow: var(--card-shadow);
      overflow: hidden;
      height: 100%; /* Allow card to fill its container */
      display: flex;
      flex-direction: column;
    }

    .card-header {
      padding: var(--card-header-footer-padding);
      border-bottom: 1px solid var(--card-border-color);
      background-color: var(--card-header-footer-bg);
    }

    .card-title {
      margin: 0;
      font-size: var(--card-title-font-size);
      color: var(--card-title-color);
    }

    .card-body {
      padding: var(--card-padding);
      flex-grow: 1; /* Allows the body to fill space, pushing the footer down */
    }

    .card-footer {
      padding: var(--card-header-footer-padding);
      border-top: 1px solid var(--card-border-color);
      background-color: var(--card-header-footer-bg);
    }
  `]
})
export class CardComponent {
  /**
   * Optional simple string to display in the card header.
   * This is ignored if [headerTemplate] is provided.
   */
  @Input() header?: string;

  /**
   * Optional simple string to display in the card footer.
   * This is ignored if [footerTemplate] is provided.
   */
  @Input() footer?: string;

  /**
   * Optional custom template to use for the card header.
   * This provides maximum flexibility for complex header content.
   */
  @Input() headerTemplate?: TemplateRef<any>;

  /**
   * Optional custom template to use for the card footer.
   */
  @Input() footerTemplate?: TemplateRef<any>;
}
