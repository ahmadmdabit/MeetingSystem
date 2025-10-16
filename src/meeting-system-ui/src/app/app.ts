import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <router-outlet />
  `,
  styles: [`
    :host {
      /*
       * Ensures the <app-root> element behaves as a standard block-level element,
       * which is a best practice for application hosts.
       */
      display: block;

      /*
       * This is critical for allowing child layouts (like MainLayoutComponent)
       * to correctly fill the entire screen height. It makes the app container
       * take up 100% of the height of its parent (the <body> tag).
       */
      height: 100%;
    }
  `]
})
export class App {
  public readonly title = signal('meeting-system-ui');
}
