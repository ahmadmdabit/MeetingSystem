import { Component, Input, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-button',
  standalone: true,
  imports: [],
  template: `
    <button
      [disabled]="disabled || isLoading"
      [class]="buttonClass"
      (click)="onClick.emit($event)"
    >
      @if(isLoading){
        <span class="spinner-border spinner-border-sm mr-1"></span>
      }
      <ng-content></ng-content>
    </button>
  `,
  styles: [`
    /*
     * Base button styles (.btn, .btn-primary, etc.) are now global.
     * Only styles for the encapsulated spinner functionality remain here.
     */
    :host {
      display: inline-block;
    }

    button {
      /* Inherit global styles but ensure it's a flex container for spinner alignment */
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }

    .mr-1 {
      margin-right: 0.25rem;
    }

    .spinner-border {
      display: inline-block;
      width: 1rem;
      height: 1rem;
      vertical-align: text-bottom;
      border: 0.2em solid currentColor;
      border-right-color: transparent;
      border-radius: 50%;
      animation: spinner-border 0.75s linear infinite;
    }

    .spinner-border-sm {
      width: 0.875rem; /* Match font-size-sm */
      height: 0.875rem;
      border-width: 0.15em;
    }

    @keyframes spinner-border {
      to {
        transform: rotate(360deg);
      }
    }
  `]
})
export class ButtonComponent {
  @Input() disabled: boolean = false;
  @Input() isLoading: boolean = false;
  @Input() variant: 'primary' | 'outline' | 'danger' | 'success' = 'primary';
  @Input() size: 'sm' | 'md' | 'lg' = 'md';

  @Output() onClick = new EventEmitter<MouseEvent>();

  get buttonClass(): string {
    const classes = [`btn-${this.variant}`];
    if (this.size === 'sm') {
      classes.push('btn-sm');
    } else if (this.size === 'lg') {
      classes.push('btn-lg');
    }
    return classes.join(' ');
  }
}
