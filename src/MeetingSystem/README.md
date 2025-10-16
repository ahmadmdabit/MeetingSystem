<p align="center">
  <a href="#" target="_blank">
    <img src="resources/images/meeting-system.png" width="200" alt="Project Logo">
  </a>
</p>

# MeetingSystem

This document provides a comprehensive technical overview of the MeetingSystem project, including its architecture, technology stack, and instructions for setup, configuration, and execution.

## Table of Contents

1.  [Architectural Overview](#1-architectural-overview)
2.  [Project Structure](#2-project-structure)
3.  [Technology Stack](#3-technology-stack)
4.  [Prerequisites](#4-prerequisites)
5.  [Configuration](#5-configuration)
6.  [Data Seeding](#6-data-seeding)
7.  [Running the Application](#7-running-the-application)
8.  [Running the Tests](#8-running-the-tests)
9.  [API Documentation](#9-api-documentation)
10. [API Use Cases](#10-api-use-cases)
11. [Database Migrations](#11-database-migrations)

## 1. Architectural Overview

The MeetingSystem project strictly adheres to the **Clean Architecture** pattern to ensure a clear separation of concerns, maintainability, and scalability. The solution is organized into four distinct layers, with a unidirectional dependency flow:

`Api` → `Business` → `Context` → `Model`

- **`MeetingSystem.Model`**: Contains the core domain entities (POCOs) and defines the data structure of the application.
- **`MeetingSystem.Context`**: Implements the data access logic using Entity Framework Core. It abstracts the database interactions through the Repository and Unit of Work patterns.
- **`MeetingSystem.Business`**: Contains all business logic, services, and orchestration. This layer is responsible for implementing the application's features and rules.
- **`MeetingSystem.Api`**: The presentation layer, built with ASP.NET Core. It exposes the application's functionality through a RESTful API and contains only minimal logic for handling HTTP requests and responses.

## 2. Project Structure

The repository is organized into the following structure:

```
/src/MeetingSystem/
├── MeetingSystem.Api/         # ASP.NET Core API Project - The application's entry point.
├── MeetingSystem.Business/      # Business Logic Layer - Contains services and DTOs.
├── MeetingSystem.Context/       # Data Access Layer - EF Core DbContext, repositories, and migrations.
├── MeetingSystem.Model/         # Domain Layer - Contains the POCO domain entities.
├── MeetingSystem.Business.Tests/ # Unit and integration tests for the Business layer.
├── resources/
│   ├── docs/                  # Contains API documentation and use case guides.
│   └── images/                # Contains images.
├── .dockerignore              # Specifies files to ignore when building Docker images.
├── .env-template              # Template for environment variables.
├── docker-compose.yml         # Defines the services for the Docker environment.
└── MeetingSystem.sln          # Visual Studio Solution file.
```

## 3. Technology Stack

- **Backend Framework:** .NET 9 (C#)
- **API Framework:** ASP.NET Core
- **Database:** Microsoft SQL Server
- **ORM:** Entity Framework Core 9
- **Background Jobs:** Hangfire
- **Object Storage:** MinIO
- **Testing:** NUnit, Moq, FluentAssertions, Testcontainers
- **Data Generation:** Bogus
- **Containerization:** Docker

## 4. Prerequisites

- .NET 9 SDK
- Docker Desktop

## 5. Configuration

The application is configured via environment variables, which are documented in the `.env-template` file. For local development, create a `.env` file in the `src/MeetingSystem` directory by copying the template:

```bash
cp .env-template .env
```

All configuration, including database connection strings, JWT secrets, and service endpoints, is managed through these variables and loaded into the application at runtime.

## 6. Data Seeding

The project includes a flexible data seeding mechanism to populate the database on startup. This is particularly useful for development and testing environments.

**Enabling Seeding:**

Set the `DK_SEEDING_ENABLED` environment variable to `true` to enable the seeder.

**Seeding Features:**

- **Default Roles:** Automatically creates the "Admin" and "User" roles if they do not exist.
- **Admin User:** Seeds a default administrator account.
- **Default Users:** Seeds a configurable number of default users.
- **Bogus Data:** Generates a configurable number of realistic but fake users and meetings using the **Bogus** library.

All seeding options can be configured via the `DK_SEEDING_*` environment variables found in the `.env-template` file.

## 7. Running the Application

The entire application stack, including the API, database, object storage, and background job server, can be started using Docker Compose.

From the `src/MeetingSystem` directory, run the following command:

```bash
docker-compose up -d
```

This will start all the services in detached mode. The API will be available at `http://localhost:8080`.

## 8. Running the Tests

The project includes a comprehensive suite of unit and integration tests. To run the tests, execute the following command from the `src/MeetingSystem` directory:

```bash
dotnet test
```

The integration tests require Docker to be running, as they use Testcontainers to spin up a real SQL Server instance.

## 9. API Documentation

The API is documented using Swagger/OpenAPI. Once the application is running, the Swagger UI can be accessed at:

`http://localhost:8080/swagger`

A static copy of the OpenAPI specification can also be found at `src/MeetingSystem/resources/docs/swagger.json`.

## 10. API Use Cases

For a practical, step-by-step guide to using the API for common workflows, please see the [API Use Cases document](./resources/docs/UseCases.md).

## 11. Database Migrations

The project uses EF Core for database migrations. To create a new migration, run the following command from the `src/MeetingSystem` directory:

```bash
dotnet ef migrations add <MigrationName> --project MeetingSystem.Context --startup-project MeetingSystem.Api
```

To apply migrations to the database, run:

```bash
dotnet ef database update --project MeetingSystem.Api
```
