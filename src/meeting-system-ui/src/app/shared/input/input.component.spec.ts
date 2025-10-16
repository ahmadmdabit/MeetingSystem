import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InputComponent } from './input.component';
import { provideZonelessChangeDetection } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';

// --- Test Host Component ---
@Component({
  standalone: true,
  imports: [InputComponent, ReactiveFormsModule],
  template: `
    <app-input
      label="Test Label"
      [formControl]="testControl"
    ></app-input>
  `
})
class TestHostComponent {
  testControl = new FormControl('', [Validators.required]);
}

describe('InputComponent', () => {
  let hostComponent: TestHostComponent;
  let fixture: ComponentFixture<TestHostComponent>;
  let nativeElement: HTMLElement;
  let inputElement: HTMLInputElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
      providers: [provideZonelessChangeDetection()]
    })
    // FIX 2: Use the 'set' property to explicitly override the providers array,
    // breaking the circular dependency for this test.
    .overrideComponent(InputComponent, {
      set: { providers: [] }
    })
    .compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    hostComponent = fixture.componentInstance;
    nativeElement = fixture.nativeElement;
    fixture.detectChanges();
    inputElement = nativeElement.querySelector('input')!;
  });

  it('should create', () => {
    expect(hostComponent).toBeTruthy();
  });

  it('should display the label', () => {
    const label = nativeElement.querySelector('label');
    expect(label?.textContent).toBe('Test Label');
  });

  it('should write value to the input from the form control', async () => {
    hostComponent.testControl.setValue('hello world');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(inputElement.value).toBe('hello world');
  });

  it('should propagate value from the input to the form control', () => {
    inputElement.value = 'new value';
    inputElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(hostComponent.testControl.value).toBe('new value');
  });

  it('should set the touched state on blur', () => {
    inputElement.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(hostComponent.testControl.touched).toBe(true);
  });

  it('should display an error message when control is invalid and touched', () => {
    let feedback = nativeElement.querySelector('.invalid-feedback');
    expect(feedback).toBeFalsy();

    hostComponent.testControl.markAsTouched();
    fixture.detectChanges();

    feedback = nativeElement.querySelector('.invalid-feedback');
    expect(inputElement.classList.contains('is-invalid')).toBe(true);
    expect(feedback).toBeTruthy();
    expect(feedback?.textContent?.trim()).toBe('Test Label is required.');
  });

  it('should be disabled when the form control is disabled', () => {
    hostComponent.testControl.disable();
    fixture.detectChanges();
    expect(inputElement.disabled).toBe(true);
  });
});
