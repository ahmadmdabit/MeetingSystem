<p align="center">
  <a href="#" target="_blank">
    <img src="public/assets/images/meeting-system.png" width="200" alt="Project Logo">
  </a>
</p>

# Meeting System UI

This project is the frontend for the Meeting System application, built with Angular 20 and TypeScript. It utilizes a fully standalone component architecture and follows modern reactive patterns.

## Table of Contents

1.  [Tech Stack](#tech-stack)
2.  [Project Structure](#project-structure)
3.  [Getting Started](#getting-started)
    *   [Prerequisites](#prerequisites)
    *   [Installation](#installation)
    *   [Environment Configuration](#environment-configuration)
4.  [API Documentation](#api-documentation)
5.  [Available Scripts](#available-scripts)
6.  [Architectural Patterns](#architectural-patterns)
7.  [Use Cases](#use-cases)
8.  [Code Scaffolding](#code-scaffolding)

## Tech Stack

*   **Framework**: [Angular](https://angular.dev/) v20.3.5
*   **Language**: [TypeScript](https://www.typescriptlang.org/) v5.9.2
*   **State Management**: [RxJS](https://rxjs.dev/) v7.8.0 (utilizing reactive view models)
*   **Styling**: SCSS with a global variable-based theme structure
*   **Package Manager**: [Yarn](https://yarnpkg.com/) v4.10.3
*   **Unit Testing**: [Karma](https://karma-runner.github.io/) and [Jasmine](https://jasmine.github.io/)

## Project Structure

The source code is organized into a logical, feature-based structure to promote modularity and scalability.

```
src/
├── app/
│   ├── core/               # Core singleton services, models, and app-wide configuration.
│   │   ├── api/            # Contains all services that communicate with the backend API.
│   │   ├── auth/           # Holds authentication-related logic like interceptors and guards.
│   │   ├── config/         # Application-wide configuration, such as the API base URL token.
│   │   └── models/         # TypeScript interfaces for all data structures (e.g., Meeting, User).
│   ├── features/           # Contains distinct business domains of the application.
│   │   ├── auth/           # Components for login and registration pages.
│   │   ├── dashboard/      # The main dashboard component displayed after login.
│   │   ├── files/          # Components for file listing and uploading.
│   │   ├── meetings/       # Components and routes for meeting management (list, detail, form).
│   │   ├── user-profile/   # Components for managing the current user's profile.
│   │   └── users/          # Components for listing and managing system users.
│   ├── layout/             # Components that define the main page structure.
│   │   └── main-layout/    # The primary authenticated layout with header, sidebar, and content outlet.
│   └── shared/             # Reusable, presentation-focused components and utilities.
│       ├── button/         # A reusable, styled button component.
│       ├── card/           # A reusable card component for displaying content blocks.
│       ├── input/          # A reusable, styled input component.
│       └── pipes/          # Reusable data-transformation pipes (e.g., meetingStatus).
├── assets/                 # Static assets like images, fonts, and icons.
├── environments/           # Environment-specific configuration files (e.g., API URLs).
└── styles/                 # Global application styles, variables, and themes.
```

## Getting Started

Follow these instructions to set up and run the project locally.

### Prerequisites

Ensure you have the following tools installed:
*   **Node.js**: v18.x or later.
*   **Yarn**: v4.x. The project is configured to use Yarn v4.10.3 via Corepack. Enable Corepack by running `corepack enable`.

### Installation

1.  **Clone the repository:**
    ```bash
    git clone <repository-url>
    cd meeting-system-ui
    ```

2.  **Install dependencies:**
    This command will install all necessary packages defined in `package.json`.
    ```bash
    yarn install
    ```

### Environment Configuration

The application requires a running backend API. The URL for this API is configured via environment files.

1.  **Development:**
    Open `src/environments/environment.ts` and ensure the `apiUrl` property points to your local backend server.
    ```typescript
    export const environment = {
      production: false,
      apiUrl: 'http://localhost:8080/api'
    };
    ```

2.  **Production:**
    Before creating a production build, open `src/environments/environment.prod.ts` and set the `apiUrl` to your production backend URL.

## API Documentation

This frontend application requires a corresponding backend API to function. The API contract is defined in the OpenAPI v3 specification located in this repository.

*   **Specification File**: `docs/swagger.json`

Before running the frontend, ensure the backend service is running and accessible at the URL configured in your `environment.ts` file.

## Available Scripts

The following scripts are available in `package.json` and can be run with `yarn <script-name>`.

*   **`yarn start`**
    Runs the application in development mode. Navigate to `http://localhost:4200/`. The app will automatically reload if you change any of the source files.

*   **`yarn build`**
    Builds the application for production. The build artifacts will be stored in the `dist/meeting-system-ui/` directory. This script uses the production environment configuration by default.

*   **`yarn test`**
    Executes the unit tests via Karma. This runs the tests once and generates a coverage report.
    ```bash
    yarn test --watch=false
    ```

*   **`yarn watch`**
    Builds the application in development mode and watches for file changes to trigger incremental rebuilds.

## Architectural Patterns

*   **Standalone Components**: The application is built exclusively with Angular's standalone components, directives, and pipes, completely eliminating the need for `NgModules`.
*   **Reactive View Models**: Components manage state using a reactive pattern. An observable `vm$` stream holds the component's state, which is consumed in the template using the `async` pipe. This improves performance and makes state management more predictable and declarative.
*   **Global Error Handling**: An `HttpInterceptor` (`error.interceptor.ts`) is implemented to globally catch and handle API errors, providing consistent user feedback and centralized logic for handling authentication errors (e.g., 401 Unauthorized).
*   **Authentication Flow**: Route access is protected using functional guards (`auth.guard.ts`). The `AuthService` handles JWT decoding and role-based access checks.

## Use Cases

For a detailed guide on the primary user flows and functionalities of the application, refer to the use case documentation.

*   **[Application Use Cases Guide](docs/UseCases.md)**

## Code Scaffolding

Leverage the Angular CLI to generate new application features.

*   **Generate a new component:**
    ```bash
    ng generate component features/my-feature/my-component --standalone
    ```

*   **Generate a new service:**
    ```bash
    ng generate service core/api/my-service
    ```