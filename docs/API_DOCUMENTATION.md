# 📖 ORBIT — REST API & SignalR Protocol Documentation

This document contains the exhaustive API specification for the **Orbit** streaming backend, including HTTP endpoints, authentication requirements, JSON payload contracts, and real-time SignalR WebSocket events.

---

## 📑 Table of Contents

1. [Authentication & Authorization Scheme](#1-authentication--authorization-scheme)
2. [Auth API (`/api/auth`)](#2-auth-api-apiauth)
3. [Accounts Management API (`/api/accounts`)](#3-accounts-management-api-apiaccounts)
4. [User Profile API (`/api/userprofile`)](#4-user-profile-api-apiuserprofile)
5. [Channel Management API (`/api/channel`)](#5-channel-management-api-apichannel)
6. [Stream Lifecycle & RTMP Callbacks (`/api/stream`)](#6-stream-lifecycle--rtmp-callbacks-apistream)
7. [Creator Studio Dashboard API (`/api/dashboard`)](#7-creator-studio-dashboard-api-apidashboard)
8. [Chat History API (`/api/chat`)](#8-chat-history-api-apichat)
9. [Channel Moderation API (`/api/moderation`)](#9-channel-moderation-api-apimoderation)
10. [Clips API (`/api/clip`)](#10-clips-api-apiclip)
11. [VODs (Video on Demand) API (`/api/vod`)](#11-vods-video-on-demand-api-apivod)
12. [Categories API (`/api/category`)](#12-categories-api-apicategory)
13. [Notifications API (`/api/notification`)](#13-notifications-api-apinotification)
14. [Admin Management API (`/api/admin`)](#14-admin-management-api-apiadmin)
15. [SignalR WebSocket Protocol (`/hubs/stream-chat`)](#15-signalr-websocket-protocol-hubsstream-chat)

---

## 1. Authentication & Authorization Scheme

- **Authentication Type:** JSON Web Token (JWT Bearer).
- **HTTP Header:**
  ```http
  Authorization: Bearer <access_token>
  ```
- **SignalR Query String:**
  ```
  /hubs/stream-chat?access_token=<access_token>
  ```
- **Role Hierarchy:**
  - `Admin`: Global administrative privileges across all users, streams, categories, and channels.
  - `Streamer`: Channel owner with broadcast privileges and studio access.
  - `User`: Standard user with watch, follow, chat, and clip creation capabilities.
- **Channel-Level Roles:**
  - `Channel Owner`: Full control over the specific channel, stream keys, and moderator team.
  - `Channel Moderator`: Mod View privileges, timeout/ban/unban powers, and message purge authority for a specific channel.

---

## 2. Auth API (`/api/auth`)

### 2.1 Register
- **Endpoint:** `POST /api/auth/register`
- **Auth:** Public
- **Body:**
  ```json
  {
    "fullName": "John Doe",
    "email": "johndoe@example.com",
    "password": "Password123!",
    "age": 22
  }
  ```
- **Response:** `200 OK`
  ```json
  {
    "id": "user-guid",
    "fullName": "John Doe",
    "email": "johndoe@example.com",
    "userName": "johndoe",
    "message": "Registration successful. Please check your email to confirm your account."
  }
  ```

### 2.2 Confirm Email (OTP)
- **Endpoint:** `POST /api/auth/confirm-email`
- **Auth:** Public
- **Body:**
  ```json
  {
    "email": "johndoe@example.com",
    "otP_Code": "X7K9LP"
  }
  ```
- **Response:** `200 OK` (Returns Auth Tokens & User Profile)

### 2.3 Login
- **Endpoint:** `POST /api/auth/login`
- **Auth:** Public
- **Body:**
  ```json
  {
    "email": "johndoe@example.com",
    "password": "Password123!"
  }
  ```
- **Response:** `200 OK`
  ```json
  {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "random-guid-token",
    "accessTokenExpiry": "2026-09-25T20:00:00Z",
    "refreshTokenExpiry": "2026-10-02T20:00:00Z",
    "user": {
      "id": "guid",
      "userName": "johndoe",
      "fullName": "John Doe",
      "email": "johndoe@example.com",
      "profilePictureUrl": "https://...",
      "roles": ["User", "Streamer"],
      "channelId": 12,
      "isOgUser": true
    }
  }
  ```

### 2.4 Google OAuth Login
- **Endpoint:** `POST /api/auth/google-login`
- **Auth:** Public
- **Body:**
  ```json
  {
    "credential": "google-id-token-jwt-from-client"
  }
  ```
- **Response:** `200 OK` (Auto-provisions user if new, marks email confirmed, returns Auth Tokens).

### 2.5 Refresh Token
- **Endpoint:** `POST /api/auth/refresh-token`
- **Auth:** Public
- **Body:**
  ```json
  {
    "refreshToken": "your-current-refresh-token"
  }
  ```

### 2.6 Forgot Password (Sends OTP)
- **Endpoint:** `POST /api/auth/forgot-password`
- **Body:** `{ "email": "user@example.com" }`

### 2.7 Reset Password (Verify OTP & Set New Password)
- **Endpoint:** `POST /api/auth/reset-password`
- **Body:**
  ```json
  {
    "email": "user@example.com",
    "otP_Code": "ABC123",
    "newPassword": "NewSecurePassword123!"
  }
  ```

---

## 3. Accounts Management API (`/api/accounts`)

- `GET /api/accounts/me` [Auth Required]: Retrieves current authenticated user's detailed profile and channel info.
- `POST /api/accounts/change-password` [Auth Required]:
  ```json
  {
    "oldPassword": "CurrentPassword123!",
    "newPassword": "BrandNewPassword123!"
  }
  ```

---

## 4. User Profile API (`/api/userprofile`)

- `POST /api/userprofile/picture` [Auth Required, Multipart/form-data]: Uploads avatar file directly to Cloudinary.
- `DELETE /api/userprofile/picture` [Auth Required]: Removes profile picture and reverts to default avatar.
- `GET /api/userprofile/{userId}` [Public]: Fetches public user details by User ID.

---

## 5. Channel Management API (`/api/channel`)

### Endpoints:
- `POST /api/channel/create` [Auth Required]: Creates a channel for the authenticated user with stream key and slug.
- `PUT /api/channel/update` [Auth Required]: Updates channel display name, bio, and social links.
  ```json
  {
    "name": "AlexGaming",
    "bio": "Daily RPG and FPS live streams!",
    "saveStreams": true,
    "socialLinks": [
      { "platform": "Twitter", "url": "https://twitter.com/alex" },
      { "platform": "YouTube", "url": "https://youtube.com/@alex" }
    ]
  }
  ```
- `GET /api/channel/{id}` [Public]: Fetches complete channel profile by Channel ID.
- `GET /api/channel/username/{username}` [Public]: Fetches complete channel profile by Streamer handle.
- `GET /api/channel/my-channel` [Auth Required]: Fetches authenticated user's channel details.
- `POST /api/channel/{id}/follow` [Auth Required]: Follows the specified channel.
- `DELETE /api/channel/{id}/follow` [Auth Required]: Unfollows the specified channel.
- `POST /api/channel/avatar` [Auth Required, Multipart]: Uploads channel profile image.
- `POST /api/channel/banner` [Auth Required, Multipart]: Uploads live profile header banner.
- `POST /api/channel/offline-banner` [Auth Required, Multipart]: Uploads offline placeholder banner.
- `POST /api/channel/{id}/moderators/{username}` [Channel Owner Only]: Appoints a user as a channel moderator.
- `DELETE /api/channel/{id}/moderators/{username}` [Channel Owner Only]: Removes moderator privileges from a user.
- `GET /api/channel/{id}/moderators` [Public/Mod]: Lists all channel moderators.
- `POST /api/channel/{id}/emotes` [Channel Owner Only, Multipart]: Uploads a custom channel emote (`imageFile`, `name`).
- `DELETE /api/channel/{id}/emotes/{emoteId}` [Channel Owner Only]: Deletes a custom channel emote.
- `GET /api/channel/{id}/emotes` [Public]: Lists all available custom emotes for this channel.

---

## 6. Stream Lifecycle & RTMP Callbacks (`/api/stream`)

### Stream Control:
- `POST /api/stream/key/generate` [Auth Required]: Generates a new cryptographically secure stream key.
- `GET /api/stream/key` [Auth Required]: Retrieves current stream key.
- `POST /api/stream/create` [Auth Required]: Prepares a new broadcast with title, category, and description.
- `PATCH /api/stream/current` [Auth Required]: Updates metadata of an ongoing broadcast in real-time.
- `POST /api/stream/end` [Auth Required]: Manually terminates active broadcast and triggers VOD finalization.
- `GET /api/stream/live` [Public]: Retrieves all currently active live streams across the platform.
- `GET /api/stream/{id}` [Public]: Retrieves stream details by Stream ID.

### NGINX-RTMP Webhooks:
- `POST /api/stream/rtmp/on-publish` [Form-urlencoded]:
  - Sent by NGINX-RTMP when OBS starts broadcasting (`name = streamKey`).
  - Returns `200 OK` if valid; `404 Not Found` if invalid stream key.
- `POST /api/stream/rtmp/on-publish-done` [Form-urlencoded]:
  - Sent by NGINX-RTMP when connection drops. Initiates the **Stream Reconnection Grace Period** (300 seconds).
- `POST /api/stream/rtmp/on-record-done` [Form-urlencoded]:
  - Sent by NGINX-RTMP when a recording file completes saving (`path = /path/to/vod.flv`).

---

## 7. Creator Studio Dashboard API (`/api/dashboard`)

- `GET /api/dashboard/summary` [Auth Required]:
  - Returns lifetime stats (total broadcast hours, peak audience, total clips, VODs, unique chatters, followers).
- `GET /api/dashboard/live-manager` [Auth Required]:
  - Returns real-time metrics for current broadcast (active viewers, duration, title, category, stream key, ingest URLs).
- `PATCH /api/dashboard/stream/current` [Auth Required]:
  - Quick-edit stream title, category ID, and description.
- `POST /api/dashboard/stream/end` [Auth Required]:
  - Ends the live broadcast session from the studio interface.
- `GET /api/dashboard/streams` [Auth Required]:
  - Retrieves broadcast history and recordings for this channel.
- `DELETE /api/dashboard/vods/{vodId}` [Auth Required]:
  - Deletes a specific VOD recording from the creator's archive.
- `GET /api/dashboard/moderation` [Auth Required]:
  - Returns list of channel moderators and active timeouts/bans.
- `GET /api/dashboard/emojis/custom` [Auth Required]:
  - Lists custom channel emotes with shortcodes.

---

## 8. Chat History API (`/api/chat`)

- `GET /api/chat/{streamId}` [Public]:
  - Returns recent non-deleted chat messages for a stream.
  - Query params: `count` (default 50).
- `GET /api/chat/{streamId}/range` [Public]:
  - Timestamp-based chat retrieval for **VOD Chat Replay**.
  - Query params: `startSeconds`, `endSeconds`.

---

## 9. Channel Moderation API (`/api/moderation`)

- `DELETE /api/moderation/{channelId}/messages/{messageId}` [Moderator / Owner]:
  - Purges a message from chat and broadcasts `MessageDeleted` via SignalR.
- `POST /api/moderation/{channelId}/timeout` [Moderator / Owner]:
  - Times out a user.
  - Body: `{ "username": "troll", "durationSeconds": 600, "reason": "Spamming" }`
- `POST /api/moderation/{channelId}/ban` [Moderator / Owner]:
  - Permanently bans a user from chatting in this channel.
  - Body: `{ "username": "troll", "reason": "Terms violation" }`
- `DELETE /api/moderation/{channelId}/ban/{username}` [Moderator / Owner]:
  - Unbans a previously banned user.
- `DELETE /api/moderation/{channelId}/timeout/{username}` [Moderator / Owner]:
  - Clears an active timeout early.
- `GET /api/moderation/{channelId}/user-status?username={username}` [Public/Mod]:
  - Returns moderation state (`isModerator`, `isTimedOut`, `timeoutRemainingSeconds`, `isBanned`).

---

## 10. Clips API (`/api/clip`)

- `POST /api/clip/slice` [Auth Required]:
  - Requests the media service to extract a 5–300s highlight from the current broadcast buffer.
  - Body:
    ```json
    {
      "streamId": 14,
      "title": "Insane Triple Kill!",
      "durationSeconds": 30
    }
    ```
- `POST /api/clip` [Auth Required, Multipart]:
  - Uploads a pre-rendered clip file directly.
- `GET /api/clip/top` [Public]:
  - Returns top clips platform-wide ranked by view count.
- `GET /api/clip/channel/{channelId}` [Public]:
  - Retrieves clips created on a specific channel.
- `GET /api/clip/{clipId}` [Public]:
  - Retrieves details of a specific clip.
- `POST /api/clip/{clipId}/view` [Public]:
  - Deduplicated view tracking for a clip (uses Auth User ID or Session ID).
- `DELETE /api/clip/{clipId}` [Auth Required: Clip Creator / Channel Owner / Admin]:
  - Deletes clip and removes file from Cloudinary.

---

## 11. VODs (Video on Demand) API (`/api/vod`)

- `GET /api/vod/channel/{channelId}` [Public]:
  - Lists completed past broadcasts for the channel that have saved recordings.
- `GET /api/vod/{vodId}` [Public]:
  - Retrieves VOD details, video URL, duration, and all timestamped chat messages for chat replay.
- `POST /api/vod/{vodId}/view` [Public]:
  - Records a unique rewatch impression for the VOD.
- `DELETE /api/vod/{vodId}` [Auth Required: Channel Owner / Admin]:
  - Deletes the VOD entry and removes recording data.

---

## 12. Categories API (`/api/category`)

- `GET /api/category` [Public]: Lists all available broadcast categories with live stream and clip counts.
- `GET /api/category/{id}` [Public]: Fetches category by ID.
- `GET /api/category/slug/{slug}` [Public]: Fetches category by URL-friendly slug (e.g., `gaming`, `just-chatting`).
- `GET /api/category/{id}/streams` [Public]: Lists active live streams under category.
- `GET /api/category/{id}/clips` [Public]: Lists top clips recorded under category.

---

## 13. Notifications API (`/api/notification`)

- `GET /api/notification` [Auth Required]: Fetches recent user notifications (e.g. followed streamer went live).
- `GET /api/notification/unread-count` [Auth Required]: Gets count of unread notifications.
- `PUT /api/notification/{id}/read` [Auth Required]: Marks a single notification as read.
- `PUT /api/notification/read-all` [Auth Required]: Marks all user notifications as read.

---

## 14. Admin Management API (`/api/admin`)

*(Requires `Admin` Role)*

- `GET /api/admin/stats`: Total users, channels, active streams, VODs, clips, categories, and chat messages.
- `GET /api/admin/users`: Paginated user list with role filters and search.
- `PUT /api/admin/users/{userId}/role`: Promotes/demotes user roles (`Admin`, `Streamer`, `User`).
- `PUT /api/admin/users/{userId}/toggle-ban`: Blocks/unblocks user from the platform.
- `GET /api/admin/channels`: List of all registered channels.
- `GET /api/admin/streams/active`: List of all ongoing broadcasts.
- `POST /api/admin/streams/{streamId}/force-end`: Administratively terminates an infringing live broadcast.
- `POST /api/admin/categories`: Creates a new platform category.
- `PUT /api/admin/categories/{id}`: Edits category name, slug, or image.
- `DELETE /api/admin/categories/{id}`: Deletes category.
- `DELETE /api/admin/clips/{clipId}`: Administratively removes a clip.
- `DELETE /api/admin/vods/{vodId}`: Administratively removes a VOD.

---

## 15. SignalR WebSocket Protocol (`/hubs/stream-chat`)

### Connection:
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/stream-chat", {
    accessTokenFactory: () => userToken
  })
  .withAutomaticReconnect()
  .build();
```

### Methods Invoked by Client:
```javascript
// 1. Join a live stream's room
await connection.invoke("JoinStream", streamId);

// 2. Send a chat message
await connection.invoke("SendMessage", streamId, "Hello chat! :orbit_fire:");

// 3. Delete a message (Mod/Broadcaster only)
await connection.invoke("DeleteMessage", streamId, messageId);

// 4. Toggle Emotes-Only mode (Mod/Broadcaster only)
await connection.invoke("SetEmotesOnly", streamId, true);

// 5. Leave stream room
await connection.invoke("LeaveStream", streamId);
```

### Events Listened to by Client:
```javascript
// New chat message
connection.on("ReceiveMessage", (messageDto) => {
  console.log(`${messageDto.senderName}: ${messageDto.content}`);
});

// Real-time viewer count changed
connection.on("ViewerCountUpdate", (streamId, viewerCount) => {
  updateViewerBadge(viewerCount);
});

// Message purged by mod
connection.on("MessageDeleted", (messageId) => {
  removeMessageFromDOM(messageId);
});

// Chatter timed out
connection.on("UserTimedOut", (username, durationSeconds) => {
  showSystemNotice(`${username} has been timed out for ${durationSeconds}s.`);
});

// Chatter banned
connection.on("UserBanned", (username) => {
  showSystemNotice(`${username} has been banned.`);
});

// Chatter unbanned
connection.on("UserUnbanned", (username) => {
  showSystemNotice(`${username} ban lifted.`);
});

// Emotes only toggled
connection.on("EmotesOnlyToggled", (isEmotesOnly) => {
  updateChatModeNotice(isEmotesOnly);
});

// Followed channel went live
connection.on("ReceiveNotification", (notification) => {
  showToastNotification(notification.title, notification.message);
});
```
