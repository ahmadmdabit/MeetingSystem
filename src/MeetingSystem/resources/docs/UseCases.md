<p align="center">
  <a href="#" target="_blank">
    <img src="../images/meeting-system.png" width="200" alt="Project Logo">
  </a>
</p>

# MeetingSystem API Use Cases

This document provides a technical, step-by-step guide for the primary use cases of the MeetingSystem API. All examples use `curl` and assume the API is running at `http://localhost:8080`.

## Prerequisites: Authentication

Most endpoints require a JSON Web Token (JWT) for authentication. The token must be passed in the `Authorization` header.

1.  **Authenticate** to get a token. For these examples, we will assume the seeded admin user (`admin@meetingsystem.local`) with the password `Password!123`.

    ```bash
    curl -s -X POST http://localhost:8080/api/auth/login -H "Content-Type: application/json" -d '{"email": "admin@meetingsystem.local", "password": "Password!123"}'
    ```

---

## Use Case 1: User Registration and Profile Management

This workflow covers the complete lifecycle of a standard user.

### Step 1: Register a New User

Send a `POST` request with the user's details as `multipart/form-data`. This supports optional profile picture uploads.

```bash
curl -X POST http://localhost:8080/api/auth/register \
     -H "Content-Type: multipart/form-data" \
     -F "FirstName=Test" \
     -F "LastName=User" \
     -F "Email=test.user@example.com" \
     -F "Phone=1234567890" \
     -F "Password=Password123!"
```

### Step 2: Log In

Authenticate with the new credentials to obtain a JWT.

```bash
curl -s -X POST http://localhost:8080/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email": "test.user@example.com", "password": "Password123!"}' 
```

### Step 3: Get User Profile

Retrieve the authenticated user's profile information.

```bash
curl -X GET http://localhost:8080/api/profile \
     -H "Authorization: Bearer $USER_TOKEN"
```

### Step 4: Update User Profile

Update the user's first name, last name, or phone number.

```bash
curl -X PUT http://localhost:8080/api/profile \
     -H "Authorization: Bearer $USER_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"firstName": "TestUpdated", "lastName": "UserUpdated", "phone": "0987654321"}'
```

---

## Use Case 2: Meeting Creation and Management

This workflow covers the lifecycle of a meeting, from creation to cancellation, as an organizer.

### Step 1: Create a New Meeting

Create a new meeting and invite participants by email. The creator is automatically assigned as the organizer.

```bash
curl -X POST http://localhost:8080/api/meetings \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
          "name": "Project Kick-off",
          "description": "Initial planning session for the new project.",
          "startAt": "'$(date -u -v+1H +'%Y-%m-%dT%H:%M:%SZ')'",
          "endAt": "'$(date -u -v+2H +'%Y-%m-%dT%H:%M:%SZ')'",
          "participantEmails": ["user@one.com", "user@two.com"]
        }'
```

### Step 2: Get a List of User's Meetings

Retrieve all meetings the authenticated user is a part of.

```bash
curl -X GET http://localhost:8080/api/meetings \
     -H "Authorization: Bearer $TOKEN"
```

### Step 3: Update a Meeting

Update the meeting's details and synchronize the participant list. The organizer cannot be removed.

```bash
# Assume MEETING_ID is retrieved from the previous step
MEETING_ID="..."

curl -X PUT http://localhost:8080/api/meetings/$MEETING_ID \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
          "name": "Project Kick-off (Updated)",
          "description": "Updated planning session.",
          "startAt": "'$(date -u -v+1H +'%Y-%m-%dT%H:%M:%SZ')'",
          "endAt": "'$(date -u -v+3H +'%Y-%m-%dT%H:%M:%SZ')'",
          "participantEmails": ["user@one.com"] # user@two.com is removed
        }'
```

### Step 4: Cancel a Meeting

As the organizer, cancel (soft delete) the meeting.

```bash
curl -X DELETE http://localhost:8080/api/meetings/$MEETING_ID \
     -H "Authorization: Bearer $TOKEN"
```

---

## Use Case 3: Administrative Actions

These actions require an administrator role.

### Step 1: List All Users

Retrieve a list of all user profiles in the system.

```bash
curl -X GET http://localhost:8080/api/users \
     -H "Authorization: Bearer $TOKEN"
```

### Step 2: Assign a Role to a User

Grant a user a new role (e.g., "Admin").

```bash
# Assume USER_ID is retrieved from the previous step
USER_ID="..."

curl -X POST http://localhost:8080/api/users/$USER_ID/roles \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"roleName": "Admin"}'
```

### Step 3: Trigger a Background Job

Manually enqueue the background job for cleaning up old, canceled meetings.

```bash
curl -X POST http://localhost:8080/api/admin/jobs/trigger-cleanup \
     -H "Authorization: Bearer $TOKEN"
```
