import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ButtonComponent } from './button.component';
import { provideZonelessChangeDetection } from '@angular/core';

describe('ButtonComponent', () => {
  let component: ButtonComponent;
  let fixture: ComponentFixture<ButtonComponent>;
  let buttonElement: HTMLButtonElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ButtonComponent],
      providers: [provideZonelessChangeDetection()]
    }).compileComponents();

    fixture = TestBed.createComponent(ButtonComponent);
    component = fixture.componentInstance;
    // Do not call detectChanges() here.
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  describe('CSS Classes', () => {
    it('should apply the primary class by default', () => {
      fixture.detectChanges();
      buttonElement = fixture.nativeElement.querySelector('button');
      expect(buttonElement.classList.contains('btn-primary')).toBe(true);
    });

    it('should apply the danger class when variant is danger', () => {
      component.variant = 'danger';
      fixture.detectChanges();
      buttonElement = fixture.nativeElement.querySelector('button');
      expect(buttonElement.classList.contains('btn-danger')).toBe(true);
    });

    it('should apply the sm class when size is sm', () => {
      component.size = 'sm';
      fixture.detectChanges();
      buttonElement = fixture.nativeElement.querySelector('button');
      expect(buttonElement.classList.contains('btn-sm')).toBe(true);
    });

    it('should apply the lg class when size is lg', () => {
      component.size = 'lg';
      fixture.detectChanges();
      buttonElement = fixture.nativeElement.querySelector('button');
      expect(buttonElement.classList.contains('btn-lg')).toBe(true);
    });
  });

  it('should emit onClick event when button is clicked', () => {
    fixture.detectChanges();
    spyOn(component.onClick, 'emit');

    buttonElement = fixture.nativeElement.querySelector('button');
    buttonElement.click();

    expect(component.onClick.emit).toHaveBeenCalled();
  });

  it('should show spinner when isLoading is true', () => {
    component.isLoading = true;
    fixture.detectChanges();

    const spinner = fixture.nativeElement.querySelector('.spinner-border');
    expect(spinner).toBeTruthy();
  });

  it('should disable button when disabled property is true', () => {
    component.disabled = true;
    fixture.detectChanges();

    buttonElement = fixture.nativeElement.querySelector('button');
    expect(buttonElement.disabled).toBe(true);
  });

  it('should disable button when isLoading is true', () => {
    component.isLoading = true;
    fixture.detectChanges();

    buttonElement = fixture.nativeElement.querySelector('button');
    // This test will now pass because of the template fix: [disabled]="disabled || isLoading"
    expect(buttonElement.disabled).toBe(true);
  });
});
