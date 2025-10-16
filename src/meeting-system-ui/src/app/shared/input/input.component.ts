import { Component, Input, forwardRef, Self, Optional } from '@angular/core';
import { ControlValueAccessor, NgControl, NG_VALUE_ACCESSOR } from '@angular/forms';

let nextId = 0;

@Component({
  selector: 'app-input',
  standalone: true,
  imports: [],
  template: `
    <div class="input-container">
      @if (label) {
        <label [for]="id" class="input-label">{{ label }}</label>
      }
      <input
        [id]="id"
        [type]="type"
        [placeholder]="placeholder"
        [readonly]="readonly"
        [value]="value"
        (input)="onInput($event)"
        (blur)="onTouched()"
        class="form-control"
        [class.is-invalid]="isInvalid"
        [attr.aria-invalid]="isInvalid"
        [disabled]="isDisabled"
      />
      @if (isInvalid && errorMessage) {
        <div class="invalid-feedback">
          {{ errorMessage }}
        </div>
      }
    </div>
  `,
  styles: [`
    :host {
      /* Ensure the custom component behaves as a block-level element */
      display: block;
    }

    .input-container {
      /* This style is specific to the component's internal structure */
      margin-bottom: 1rem; /* var(--spacing-base) */
    }

    /*
     * All styles for .input-label, .form-control, .is-invalid,
     * and .invalid-feedback are now inherited from the global
     * styles.scss and have been safely removed from this component.
     */
  `],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => InputComponent),
      multi: true
    }
  ]
})
export class InputComponent implements ControlValueAccessor {
  // --- Component Inputs ---
  @Input() id: string = `app-input-${nextId++}`;
  @Input() type: string = 'text';
  @Input() label: string = '';
  @Input() placeholder: string = '';

  // FIX: Use a setter to transform the boolean input for readonly.
  // This is the classic, universally compatible way to handle boolean attributes.
  private _readonly: boolean = false;
  @Input()
  get readonly(): boolean {
    return this._readonly;
  }
  set readonly(value: any) {
    this._readonly = value != null && `${value}` !== 'false';
  }

  // --- Internal State ---
  protected value: string = '';
  protected isDisabled: boolean = false;

  // --- ControlValueAccessor Callbacks ---
  private onChange = (value: string) => {};
  // FIX: Use 'protected' so the template can access it.
  protected onTouched = () => {};

  constructor(@Optional() @Self() private ngControl: NgControl) {
    if (this.ngControl) {
      this.ngControl.valueAccessor = this;
    }
  }

  get isInvalid(): boolean {
    return !!(this.ngControl?.invalid && (this.ngControl.touched || this.ngControl.dirty));
  }

  get errorMessage(): string | null {
    if (!this.ngControl?.errors) {
      return null;
    }
    const errors = this.ngControl.errors;
    if (errors['required']) {
      return `${this.label || 'This field'} is required.`;
    }
    if (errors['email']) {
      return 'Please enter a valid email address.';
    }
    return 'This field is invalid.';
  }

  // --- ControlValueAccessor Implementation ---

  writeValue(value: any): void {
    this.value = value || '';
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
  }

  // --- DOM Event Handlers ---

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.value = value;
    this.onChange(value);
  }
}
