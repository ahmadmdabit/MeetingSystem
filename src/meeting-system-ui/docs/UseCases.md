<p align="center">
  <a href="#" target="_blank">
    <img src="../public/assets/images/meeting-system.png" width="200" alt="Project Logo">
  </a>
</p>

# Meeting System UI Application Use Cases

This document outlines the step-by-step execution of primary use cases within the Meeting System UI. It is intended for technical stakeholders, developers, and QA testers.

## Table of Contents

1.  [User Registration](#1-user-registration)
2.  [User Login](#2-user-login)
3.  [Meeting Creation](#3-meeting-creation)
4.  [Meeting Viewing and Management](#4-meeting-viewing-and-management)
5.  [Meeting Editing and File Attachment](#5-meeting-editing-and-file-attachment)
6.  [User Profile Management](#6-user-profile-management)

---

### 1. User Registration

This use case describes the process for a new user creating an account.

1.  **Navigation**: The user navigates to the `/register` endpoint.
2.  **Data Input**: The user populates the registration form with valid data, including First Name, Last Name, Email, and a matching Password and Confirm Password.
3.  **Initiate Action**: The user clicks the **Register** button.
4.  **System Process**:
    *   The application constructs a `RegisterUser` payload.
    *   A `POST` request is sent to the `/api/Auth/register` endpoint.
5.  **Successful Outcome**: Upon receiving a `200 OK` response, the user is redirected to `/login?registered=true`, which displays a success message.
6.  **Failure Outcome**: If the API returns a `400 Bad Request` (e.g., validation error, email already exists), the corresponding error message is displayed on the registration form.

### 2. User Login

This use case describes how an existing user authenticates with the system.

1.  **Navigation**: The user navigates to the `/login` endpoint.
2.  **Data Input**: The user enters their registered email and password.
3.  **Initiate Action**: The user clicks the **Login** button.
4.  **System Process**:
    *   The application constructs a `Login` payload.
    *   A `POST` request is sent to the `/api/Auth/login` endpoint.
5.  **Successful Outcome**: Upon receiving a `200 OK` response containing a JWT, the application:
    *   Stores the JWT in the browser's `localStorage` under the key `auth_token`.
    *   Redirects the user to the `/dashboard`.
6.  **Failure Outcome**: If the API returns a `401 Unauthorized` response, an "Invalid credentials" error message is displayed on the login form.

### 3. Meeting Creation

This use case covers the creation of a new meeting by an authenticated user.

1.  **Precondition**: The user must be authenticated.
2.  **Navigation**: The user navigates to the meeting list at `/meetings`.
3.  **Initiate Action**: The user clicks the **Create Meeting** button.
4.  **System Process**: The application navigates to the meeting form at `/meetings/new`.
5.  **Data Input**: The user fills in the meeting details, including Name, Description, Start Time, and End Time.
6.  **Initiate Action**: The user clicks the **Create Meeting** button.
7.  **System Process**:
    *   The application constructs a `CreateMeeting` payload. The `startAt` and `endAt` values, read from the `datetime-local` input, are converted to UTC ISO 8601 strings.
    *   A `POST` request is sent to the `/api/meetings` endpoint.
8.  **Successful Outcome**: Upon receiving a `201 Created` response with the new meeting object, the application redirects the user to the edit page for the newly created meeting (`/meetings/:id/edit`) to allow for immediate file attachments or further edits.

### 4. Meeting Viewing and Management

This use case describes how a user views and interacts with the list of meetings.

1.  **Precondition**: The user must be authenticated.
2.  **Navigation**: The user navigates to `/meetings`.
3.  **System Process**:
    *   The `MeetingsListComponent` triggers an API call (`GET /api/meetings`).
    *   The returned array of meetings is processed by an RxJS stream, which first sorts the meetings by `startAt` date (ascending) and then groups them into categories: `inProgress`, `upcoming`, `finished`, and `canceled`.
4.  **Successful Outcome**: The UI renders distinct sections for each category, displaying the sorted meetings within them.
5.  **User Interaction**:
    *   Clicking **View** on a meeting card navigates the user to the detail page at `/meetings/:id`.
    *   Clicking **Edit** on a meeting card navigates the user to the edit form at `/meetings/:id/edit`.
    *   Clicking **Cancel** on a meeting card shows a browser confirmation prompt. If confirmed, a `DELETE` request is sent to `/api/meetings/:id`, and upon success, the meeting list is refreshed.

### 5. Meeting Editing and File Attachment

This use case covers modifying an existing meeting and attaching files.

1.  **Precondition**: The user must be authenticated and be the meeting organizer or an admin.
2.  **Navigation**: The user navigates to the edit form at `/meetings/:id/edit`.
3.  **System Process**: The form is pre-populated with data from a `GET /api/meetings/:id` request. UTC dates are converted to the user's local time for display in the `datetime-local` inputs.
4.  **Data Input**:
    *   The user modifies one or more fields in the meeting form.
    *   The user interacts with the `app-file-upload` component, selecting one or more files from their local system.
5.  **Initiate Action**: The user clicks the **Update Meeting** button.
6.  **System Process**:
    *   A `PUT` request containing the `UpdateMeeting` payload is sent to `/api/meetings/:id`.
    *   If files were selected, upon successful completion of the `PUT` request, a `POST` request (multipart/form-data) containing the files is sent to `/api/meetings/:id/files`.
7.  **Successful Outcome**: The form remains on the edit page. The `app-file-list` component refreshes to display the newly uploaded files.

### 6. User Profile Management

This use case describes how a user updates their own profile information.

1.  **Precondition**: The user must be authenticated.
2.  **Navigation**: The user navigates to the profile page via the main layout's navigation, landing at `/profile`.
3.  **System Process**: The `UserProfileComponent` fetches the current user's data via a `GET /api/users/me` request and populates the form.
4.  **Data Input**: The user modifies their First Name, Last Name, or Phone number.
5.  **Initiate Action**: The user clicks the **Update Profile** button.
6.  **System Process**: A `PUT` request with the `UpdateUserProfile` payload is sent to `/api/users/me`.
7.  **Successful Outcome**: A success message is displayed, and the form fields are updated with the new data.
