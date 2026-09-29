# Cursor prompt — User Management UI (paste into frontend repo)

Copy everything below the line into Cursor in the Kingsight SPA project.

---

Implement or update the **User Management** admin screen to use the Kingsight API user-management endpoints. User and role primary keys are **sequential integers** (`userId` / `roleId`: 1, 2, 3…). They are **not** strings, GUIDs, or large snowflake numbers. The grid must show **User ID** as the first column.

## API base

`{apiBaseUrl}/api/user-management`

All JSON is **camelCase**. Dates are ISO-8601 UTC strings.

## TypeScript models

```typescript
export interface RoleDto {
  roleId: number;
  roleName: string;
  status: string | null;
}

export interface UserDto {
  userId: number;
  email: string;
  firstName: string | null;
  lastName: string | null;
  isActive: boolean;
  dateCreated: string;
  dateModified: string | null;
  roleId: number;
  roleName: string;
}

export interface RoleSaveRequest {
  roleName: string;
  status?: string | null;
}

export interface UserSaveRequest {
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  isActive: boolean;
  roleId: number;
}

// Update bodies match save bodies (no userId / roleId in body)
export type RoleUpdateRequest = RoleSaveRequest;
export type UserUpdateRequest = UserSaveRequest;
```

## Endpoints

| Action | Method | URL | Body | Response |
|--------|--------|-----|------|----------|
| List roles | GET | `/roles` | — | `RoleDto[]` |
| Get role | GET | `/roles/{roleId}` | — | `RoleDto` |
| Create role | POST | `/roles` | `RoleSaveRequest` | **201** `RoleDto` with `roleId` |
| Update role | PUT | `/roles/{roleId}` | `RoleUpdateRequest` | **200** `RoleDto` |
| Delete role | DELETE | `/roles/{roleId}` | — | **204** |
| List users | GET | `/users` | — | `UserDto[]` |
| Get user | GET | `/users/{userId}` | — | `UserDto` |
| Create user | POST | `/users` | `UserSaveRequest` | **201** `UserDto` with `userId` |
| Update user | PUT | `/users/{userId}` | `UserUpdateRequest` | **200** `UserDto` |
| Delete user | DELETE | `/users/{userId}` | — | **204** |

## UI requirements

### Users table
- Columns (in order): **User ID** (`userId`), Email, First Name, Last Name, Active, Role (`roleName`), Date Created, actions (Edit / Delete).
- Use `userId` as the **row key** / `trackBy` / DataGrid `getRowId` — never email or array index.
- After **create**, read `response.userId` from the POST response body and add/update the row (or refetch list).
- After **update**, read `response.userId` from the PUT response body (same id as URL) and patch local state.
- **Delete** calls `DELETE /users/{userId}` using the row’s `userId`.

### User form (add / edit)
- **Create:** POST body must **not** include `userId`. Server assigns the next id.
- **Edit:** PUT `/users/{userId}` — `userId` only in the URL, not in the body. Keep `userId` in component state from the selected table row.
- Role dropdown: load from `GET /roles`, bind `roleId` (number), display `roleName`.
- Validate email format client-side; show API **400** messages (duplicate email, invalid role).

### Roles (if exposed in same module)
- Same pattern: show `roleId` column, use `roleId` in PUT/DELETE URLs, POST does not send `roleId`.

## Example service (Angular-style)

```typescript
@Injectable({ providedIn: 'root' })
export class UserManagementService {
  private base = `${environment.apiUrl}/api/user-management`;

  getUsers() {
    return this.http.get<UserDto[]>(`${this.base}/users`);
  }

  createUser(body: UserSaveRequest) {
    return this.http.post<UserDto>(`${this.base}/users`, body);
  }

  updateUser(userId: number, body: UserUpdateRequest) {
    return this.http.put<UserDto>(`${this.base}/users/${userId}`, body);
  }

  deleteUser(userId: number) {
    return this.http.delete<void>(`${this.base}/users/${userId}`);
  }

  getRoles() {
    return this.http.get<RoleDto[]>(`${this.base}/roles`);
  }
}
```

## Common mistakes to avoid
- Treating `userId` as `string` or `bigint` — use `number`.
- Omitting `userId` from the table — product expects it visible like the database.
- Sending `userId` on POST create.
- Using email or row index when calling PUT/DELETE instead of `userId`.
- Parsing ids from JWT or auth profile — always use API `userId` from `UserMst`.

## Seed data reference (for local testing)
- Roles: 1 Administrator, 2 Kingsett User, 3 User A
- Users: userIds 1–5, all `roleId: 1` (Administrator)

Match existing app patterns (shared HTTP client, error toast, Material table or AG Grid, routing under admin/settings).
