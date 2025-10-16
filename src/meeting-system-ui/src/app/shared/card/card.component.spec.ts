import { Component, TemplateRef, ViewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CardComponent } from './card.component';
import { provideZonelessChangeDetection } from '@angular/core';

// --- Test Host Component ---
@Component({
  standalone: true,
  imports: [CardComponent],
  template: `
    <app-card
      [header]="header"
      [footer]="footer"
      [headerTemplate]="headerTpl"
      [footerTemplate]="footerTpl"
      [class]="customClass"
    >
      <p class="projected-content">This is the projected content.</p>
    </app-card>

    <ng-template #testHeaderTpl>
      <div class="custom-header">Custom Header Template</div>
    </ng-template>

    <ng-template #testFooterTpl>
      <div class="custom-footer">Custom Footer Template</div>
    </ng-template>
  `
})
class TestHostComponent {
  header?: string;
  footer?: string;
  headerTpl?: TemplateRef<any>;
  footerTpl?: TemplateRef<any>;
  customClass?: string;

  @ViewChild('testHeaderTpl', { static: true })
  headerTemplateRef!: TemplateRef<any>;

  @ViewChild('testFooterTpl', { static: true })
  footerTemplateRef!: TemplateRef<any>;
}

describe('CardComponent', () => {
  let hostComponent: TestHostComponent;
  let fixture: ComponentFixture<TestHostComponent>;
  let nativeElement: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
      providers: [provideZonelessChangeDetection()]
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    hostComponent = fixture.componentInstance;
    nativeElement = fixture.nativeElement;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(hostComponent).toBeTruthy();
  });

  describe('Simple String Inputs', () => {
    it('should render the header when [header] text is provided', () => {
      hostComponent.header = 'Test Header';
      fixture.detectChanges();

      const header = nativeElement.querySelector('.card-header');
      const title = nativeElement.querySelector('.card-title');

      expect(header).toBeTruthy();
      expect(title?.textContent?.trim()).toBe('Test Header');
    });

    it('should not render the header when no header is provided', () => {
      hostComponent.header = undefined;
      fixture.detectChanges();

      const header = nativeElement.querySelector('.card-header');
      expect(header).toBeFalsy();
    });

    it('should render the footer when [footer] text is provided', () => {
      hostComponent.footer = 'Test Footer';
      fixture.detectChanges();

      const footer = nativeElement.querySelector('.card-footer');
      expect(footer).toBeTruthy();
      expect(footer?.textContent?.trim()).toBe('Test Footer');
    });

    it('should not render the footer when no footer is provided', () => {
      hostComponent.footer = undefined;
      fixture.detectChanges();

      const footer = nativeElement.querySelector('.card-footer');
      expect(footer).toBeFalsy();
    });
  });

  describe('Content and Template Projection', () => {
    it('should project content into the card body using <ng-content>', () => {
      // FIX: Call detectChanges() to render the component's initial state.
      fixture.detectChanges();

      const body = nativeElement.querySelector('.card-body');
      const projectedContent = nativeElement.querySelector('.projected-content');

      expect(body).toBeTruthy();
      expect(projectedContent).toBeTruthy();
      expect(projectedContent?.textContent).toBe('This is the projected content.');
    });

    it('should render a custom header from a TemplateRef', () => {
      hostComponent.headerTpl = hostComponent.headerTemplateRef;
      fixture.detectChanges();

      const header = nativeElement.querySelector('.card-header');
      const customHeader = nativeElement.querySelector('.custom-header');

      expect(header).toBeTruthy();
      expect(customHeader).toBeTruthy();
      expect(customHeader?.textContent).toBe('Custom Header Template');
      expect(nativeElement.querySelector('.card-title')).toBeFalsy();
    });

    it('should render a custom footer from a TemplateRef', () => {
      hostComponent.footerTpl = hostComponent.footerTemplateRef;
      fixture.detectChanges();

      const footer = nativeElement.querySelector('.card-footer');
      const customFooter = nativeElement.querySelector('.custom-footer');

      expect(footer).toBeTruthy();
      expect(customFooter).toBeTruthy();
      expect(customFooter?.textContent).toBe('Custom Footer Template');
    });

    it('should prioritize the template over the simple string input for the header', () => {
      hostComponent.header = 'This should be ignored';
      hostComponent.headerTpl = hostComponent.headerTemplateRef;
      fixture.detectChanges();

      const customHeader = nativeElement.querySelector('.custom-header');
      expect(customHeader).toBeTruthy();
      expect(nativeElement.querySelector('.card-title')).toBeFalsy();
    });
  });

  describe('Host Bindings and Attributes', () => {
    it('should apply a custom CSS class to the host element', () => {
      hostComponent.customClass = 'custom-card-class';
      fixture.detectChanges();

      const cardHostElement = nativeElement.querySelector('app-card');
      expect(cardHostElement?.classList.contains('custom-card-class')).toBe(true);
    });
  });
});
